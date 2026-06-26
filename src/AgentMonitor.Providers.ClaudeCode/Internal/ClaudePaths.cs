namespace AgentMonitor.Providers.ClaudeCode.Internal;

/// <summary>
/// Resolves the on-disk locations Claude Code uses. Isolated so that a future
/// layout change in Claude Code is a one-file fix.
/// </summary>
internal sealed class ClaudePaths
{
    public string Root { get; }
    public string SessionsDir => Path.Combine(Root, "sessions");
    public string ProjectsDir => Path.Combine(Root, "projects");

    /// <summary>Where the hook sink writes per-session status markers.</summary>
    public string HookStatusDir => Path.Combine(Root, "agent-monitor", "status");

    public string HookStatusFile(string sessionId)
        => Path.Combine(HookStatusDir, sessionId + ".json");

    public ClaudePaths(string? rootOverride = null)
    {
        Root = rootOverride
            ?? Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");
    }

    public bool Exists => Directory.Exists(Root);

    /// <summary>Best-effort transcript path for a session given its cwd and id.</summary>
    public string? FindTranscript(string sessionId, string? cwd)
    {
        if (cwd is not null)
        {
            var candidate = Path.Combine(ProjectsDir, SlugForCwd(cwd), sessionId + ".jsonl");
            if (File.Exists(candidate))
                return candidate;
        }

        // Fall back to a search across all project folders (sessionId is unique).
        if (!Directory.Exists(ProjectsDir))
            return null;

        try
        {
            foreach (var dir in Directory.EnumerateDirectories(ProjectsDir))
            {
                var candidate = Path.Combine(dir, sessionId + ".jsonl");
                if (File.Exists(candidate))
                    return candidate;
            }
        }
        catch
        {
            // ignore enumeration failures
        }

        return null;
    }

    /// <summary>
    /// Mirrors Claude Code's project-folder slug, where drive and separator
    /// characters become '-'. e.g. "D:\TodoListMcp" => "D--TodoListMcp".
    /// </summary>
    public static string SlugForCwd(string cwd)
        => cwd.Replace(':', '-').Replace('\\', '-').Replace('/', '-');
}
