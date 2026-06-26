using System.Text.Json;
using AgentMonitor.Core.Sessions;
using AgentMonitor.Providers.ClaudeCode;
using AgentMonitor.Providers.Codex;

// ---------------------------------------------------------------------------
// Part A: dump what the providers see against the real data right now.
// ---------------------------------------------------------------------------
Console.WriteLine("== Live snapshot (Claude Code) ==");
var live = new ClaudeCodeProvider();
Console.WriteLine($"Claude installed: {live.IsInstalled}, hooks active: {live.HooksActive}");
foreach (var s in live.GetSessions())
    Console.WriteLine($"  - {s.Title,-22} {s.Status,-13} pid={s.ProcessId} detail={s.Detail}");

Console.WriteLine("\n== Live snapshot (Codex, default 15-min window) ==");
var codex = new CodexProvider();
Console.WriteLine($"Codex installed: {codex.IsInstalled}");
foreach (var s in codex.GetSessions())
    Console.WriteLine($"  - {s.Title,-22} {s.Status,-13} detail={s.Detail} origin={s.Origin}");

Console.WriteLine("\n== Codex parse check (wide 90-day window — proves rollout parsing) ==");
var codexWide = new CodexProvider(recencyWindow: TimeSpan.FromDays(90));
foreach (var s in codexWide.GetSessions().Take(8))
    Console.WriteLine($"  - {s.Title,-22} {s.Status,-13} detail={s.Detail} cwd={s.WorkingDirectory}");

// ---------------------------------------------------------------------------
// Part B: deterministic test of the hook interpreter. We stage a temp config
// dir around a REAL live claude PID (so the liveness check passes), give it an
// OLD transcript resting at tool_use, then drive hook markers and assert.
// ---------------------------------------------------------------------------
Console.WriteLine("\n== Hook interpreter test ==");

var realSessions = Directory.GetFiles(
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "sessions"),
    "*.json");

JsonElement? pick = null;
foreach (var f in realSessions)
{
    var el = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(f));
    if (el.TryGetProperty("pid", out var pidEl))
    {
        try { using var _ = System.Diagnostics.Process.GetProcessById(pidEl.GetInt32()); pick = el; break; }
        catch { }
    }
}

if (pick is null)
{
    Console.WriteLine("No live session to borrow a PID from — skipping.");
    return;
}

var rec = pick.Value;
int pid = rec.GetProperty("pid").GetInt32();
string sid = rec.GetProperty("sessionId").GetString()!;
string cwd = rec.GetProperty("cwd").GetString()!;
string slug = cwd.Replace(':', '-').Replace('\\', '-').Replace('/', '-');

string root = Path.Combine(Path.GetTempPath(), "agentmon-test-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(Path.Combine(root, "sessions"));
Directory.CreateDirectory(Path.Combine(root, "projects", slug));
Directory.CreateDirectory(Path.Combine(root, "agent-monitor", "status"));

File.WriteAllText(Path.Combine(root, "sessions", $"{pid}.json"), rec.GetRawText());
string transcript = Path.Combine(root, "projects", slug, sid + ".jsonl");
File.WriteAllText(transcript, "{\"type\":\"assistant\",\"message\":{\"stop_reason\":\"tool_use\"}}\n");

string statusFile = Path.Combine(root, "agent-monitor", "status", sid + ".json");
var oldTime = DateTime.UtcNow.AddMinutes(-5);

void SetTranscript(DateTime utc) => File.SetLastWriteTimeUtc(transcript, utc);
void WriteMarker(string evt, string? msg, DateTimeOffset ts) => File.WriteAllText(statusFile,
    JsonSerializer.Serialize(new { sessionId = sid, @event = evt, message = msg, timestamp = ts.ToString("o") }));

SessionStatus StatusNow()
{
    var p = new ClaudeCodeProvider(root);
    var list = p.GetSessions();
    return list.Count == 1 ? list[0].Status : SessionStatus.Unknown;
}

int pass = 0, fail = 0;
void Check(string name, SessionStatus actual, SessionStatus expected)
{
    bool ok = actual == expected;
    if (ok) pass++; else fail++;
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name,-46} got {actual}, want {expected}");
}

// 1. No marker, old tool_use transcript -> heuristic says idle (AwaitingInput).
SetTranscript(oldTime);
if (File.Exists(statusFile)) File.Delete(statusFile);
Check("no hooks -> heuristic idle", StatusNow(), SessionStatus.AwaitingInput);

// 2. UserPromptSubmit marker -> Working (hook overrides idle heuristic).
SetTranscript(oldTime);
WriteMarker("UserPromptSubmit", null, DateTime.UtcNow);
Check("UserPromptSubmit -> working", StatusNow(), SessionStatus.Working);

// 3. Stop marker, transcript older than marker -> AwaitingInput (finished).
SetTranscript(oldTime);
WriteMarker("Stop", null, DateTime.UtcNow);
Check("Stop -> awaiting (finished)", StatusNow(), SessionStatus.AwaitingInput);

// 4. Notification marker -> AwaitingInput (needs attention).
SetTranscript(oldTime);
WriteMarker("Notification", "needs permission", DateTime.UtcNow);
Check("Notification -> awaiting", StatusNow(), SessionStatus.AwaitingInput);

// 5. Stop marker but transcript written AFTER it -> resumed -> Working.
WriteMarker("Stop", null, DateTimeOffset.UtcNow.AddSeconds(-30));
SetTranscript(DateTime.UtcNow);
Check("Stop then activity -> working (resumed)", StatusNow(), SessionStatus.Working);

try { Directory.Delete(root, recursive: true); } catch { }

Console.WriteLine($"\n{pass} passed, {fail} failed.");
Environment.Exit(fail == 0 ? 0 : 1);
