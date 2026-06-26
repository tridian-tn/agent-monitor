using System.Text.Json;
using System.Text.Json.Nodes;
using AgentMonitor.Tray;

// Stage a temp config dir with a pre-existing, unrelated hook + setting, then
// exercise install / idempotency / uninstall, asserting nothing else is clobbered.

string root = Path.Combine(Path.GetTempPath(), "agentmon-install-" + Guid.NewGuid().ToString("N")[..8]);
Directory.CreateDirectory(root);
string settings = Path.Combine(root, "settings.json");
Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", root);

// A dummy sink exe next to this test so the installer resolves a command.
File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "AgentMonitor.HookSink.exe"), "stub");

// Seed an existing setting and an existing unrelated Stop hook.
File.WriteAllText(settings, """
{
  "model": "opus",
  "hooks": {
    "Stop": [
      { "hooks": [ { "type": "command", "command": "echo existing" } ] }
    ]
  }
}
""");

int pass = 0, fail = 0;
void Check(string name, bool ok)
{
    if (ok) pass++; else fail++;
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}");
}

JsonObject Read() => (JsonObject)JsonNode.Parse(File.ReadAllText(settings))!;
int CommandCount(JsonObject o, string evt) =>
    (o["hooks"]?[evt] as JsonArray)?.Count ?? 0;
bool HasCommand(JsonObject o, string evt, string contains)
    => (o["hooks"]?[evt] as JsonArray)?
        .SelectMany(g => (g?["hooks"] as JsonArray) ?? new JsonArray())
        .Any(h => (h?["command"]?.GetValue<string>() ?? "").Contains(contains)) ?? false;

var inst = new HookInstaller();
Check("can install (sink resolved)", inst.CanInstall);
Check("not installed initially", !inst.IsInstalled());

inst.Install();
var afterInstall = Read();
Check("installed flag true", inst.IsInstalled());
Check("UserPromptSubmit hook added", HasCommand(afterInstall, "UserPromptSubmit", "AgentMonitor.HookSink"));
Check("Notification hook added", HasCommand(afterInstall, "Notification", "AgentMonitor.HookSink"));
Check("SessionEnd hook added", HasCommand(afterInstall, "SessionEnd", "AgentMonitor.HookSink"));
Check("existing Stop hook preserved", HasCommand(afterInstall, "Stop", "echo existing"));
Check("our Stop hook also present", HasCommand(afterInstall, "Stop", "AgentMonitor.HookSink"));
Check("unrelated setting preserved", afterInstall["model"]?.GetValue<string>() == "opus");
Check("backup written", File.Exists(settings + ".bak"));

int stopCountAfterFirst = CommandCount(afterInstall, "Stop");
inst.Install(); // idempotent
Check("re-install is idempotent (no dupes)", CommandCount(Read(), "Stop") == stopCountAfterFirst);

inst.Uninstall();
var afterRemove = Read();
Check("not installed after uninstall", !inst.IsInstalled());
Check("our hooks removed", !HasCommand(afterRemove, "UserPromptSubmit", "AgentMonitor.HookSink"));
Check("existing Stop hook still preserved", HasCommand(afterRemove, "Stop", "echo existing"));
Check("unrelated setting still preserved", afterRemove["model"]?.GetValue<string>() == "opus");

try { Directory.Delete(root, true); } catch { }
Console.WriteLine($"\n{pass} passed, {fail} failed.");
Environment.Exit(fail == 0 ? 0 : 1);
