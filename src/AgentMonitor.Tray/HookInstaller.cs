using System.Text.Json;
using System.Text.Json.Nodes;

namespace AgentMonitor.Tray;

/// <summary>
/// Installs and removes the Agent Monitor hook entries in Claude Code's
/// user <c>settings.json</c>. Merges non-destructively (preserving any existing
/// hooks) and takes a backup before writing.
/// </summary>
internal sealed class HookInstaller
{
    private static readonly string[] Events =
    {
        "UserPromptSubmit", "Stop", "Notification", "SessionEnd",
    };

    public string SettingsPath { get; }
    public string? SinkExePath { get; }

    public HookInstaller()
    {
        var root = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
        SettingsPath = Path.Combine(root, "settings.json");
        SinkExePath = ResolveSinkExe();
    }

    /// <summary>The quoted command Claude Code will run for each hook event.</summary>
    public string? Command => SinkExePath is null ? null : $"\"{SinkExePath}\"";

    public bool CanInstall => SinkExePath is not null;

    public bool IsInstalled()
    {
        if (Command is null || !File.Exists(SettingsPath))
            return false;
        try
        {
            var root = JsonNode.Parse(File.ReadAllText(SettingsPath)) as JsonObject;
            var hooks = root?["hooks"] as JsonObject;
            if (hooks is null)
                return false;
            return Events.Any(e => GroupsFor(hooks, e) is { } arr && ContainsCommand(arr, Command));
        }
        catch
        {
            return false;
        }
    }

    public void Install()
    {
        if (Command is null)
            throw new InvalidOperationException("Hook sink executable not found.");

        var root = ReadSettings();
        var hooks = GetOrAddObject(root, "hooks");

        foreach (var evt in Events)
        {
            var groups = hooks[evt] as JsonArray;
            if (groups is null)
            {
                groups = new JsonArray();
                hooks[evt] = groups;
            }

            if (!ContainsCommand(groups, Command))
            {
                groups.Add(new JsonObject
                {
                    ["hooks"] = new JsonArray
                    {
                        new JsonObject { ["type"] = "command", ["command"] = Command },
                    },
                });
            }
        }

        WriteSettings(root);
    }

    public void Uninstall()
    {
        if (Command is null || !File.Exists(SettingsPath))
            return;

        var root = ReadSettings();
        if (root["hooks"] is not JsonObject hooks)
            return;

        foreach (var evt in Events)
        {
            if (hooks[evt] is not JsonArray groups)
                continue;

            for (int i = groups.Count - 1; i >= 0; i--)
            {
                if (groups[i] is JsonObject group
                    && group["hooks"] is JsonArray inner
                    && ContainsCommandInner(inner, Command))
                {
                    groups.RemoveAt(i);
                }
            }

            if (groups.Count == 0)
                hooks.Remove(evt);
        }

        if (hooks.Count == 0)
            root.Remove("hooks");

        WriteSettings(root);
    }

    private JsonObject ReadSettings()
    {
        if (!File.Exists(SettingsPath))
            return new JsonObject();
        try
        {
            return JsonNode.Parse(File.ReadAllText(SettingsPath)) as JsonObject ?? new JsonObject();
        }
        catch
        {
            return new JsonObject();
        }
    }

    private void WriteSettings(JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        if (File.Exists(SettingsPath))
            File.Copy(SettingsPath, SettingsPath + ".bak", overwrite: true);

        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    private static JsonArray? GroupsFor(JsonObject hooks, string evt) => hooks[evt] as JsonArray;

    private static JsonObject GetOrAddObject(JsonObject parent, string name)
    {
        if (parent[name] is JsonObject existing)
            return existing;
        var created = new JsonObject();
        parent[name] = created;
        return created;
    }

    private static bool ContainsCommand(JsonArray groups, string command)
        => groups.OfType<JsonObject>()
            .Any(g => g["hooks"] is JsonArray inner && ContainsCommandInner(inner, command));

    private static bool ContainsCommandInner(JsonArray inner, string command)
        => inner.OfType<JsonObject>()
            .Any(h => h["command"]?.GetValue<string>() == command);

    private static string? ResolveSinkExe()
    {
        const string exeName = "AgentMonitor.HookSink.exe";
        var candidates = new[]
        {
            // 1. Next to the tray exe (post-build copy / published layout).
            Path.Combine(AppContext.BaseDirectory, exeName),
            // 2. Sibling project build output (running from a dev tree).
            Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", "..", "..", "..",
                "AgentMonitor.HookSink", "bin",
#if DEBUG
                "Debug",
#else
                "Release",
#endif
                "net10.0", exeName)),
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
