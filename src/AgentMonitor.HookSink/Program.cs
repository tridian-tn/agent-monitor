using System.Text.Json;

// Invoked by Claude Code hooks. Reads the hook payload JSON from stdin and writes
// a per-session status file under ~/.claude/agent-monitor/status that the tray
// reads. Must be fast and must never disrupt Claude: all failures are swallowed
// and we always exit 0.

string input;
try
{
    input = Console.In.ReadToEnd();
}
catch
{
    return 0;
}

try
{
    using var doc = JsonDocument.Parse(input);
    var root = doc.RootElement;

    string? sessionId = GetString(root, "session_id");
    string? evt = GetString(root, "hook_event_name");
    if (string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(evt))
        return 0;

    string statusDir = Path.Combine(ResolveClaudeRoot(), "agent-monitor", "status");
    Directory.CreateDirectory(statusDir);
    string file = Path.Combine(statusDir, sessionId + ".json");

    // Session ended: drop the marker so the tray stops reporting it.
    if (evt == "SessionEnd")
    {
        try { File.Delete(file); } catch { /* ignore */ }
        return 0;
    }

    var status = new Dictionary<string, object?>
    {
        ["sessionId"] = sessionId,
        ["event"] = evt,
        ["message"] = GetString(root, "message"),
        ["cwd"] = GetString(root, "cwd"),
        ["timestamp"] = DateTimeOffset.UtcNow.ToString("o"),
    };

    File.WriteAllText(file, JsonSerializer.Serialize(status));
}
catch
{
    // Never let a hook failure surface to Claude.
}

return 0;

static string? GetString(JsonElement root, string name)
    => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        ? value.GetString()
        : null;

static string ResolveClaudeRoot()
    => Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR")
        ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
