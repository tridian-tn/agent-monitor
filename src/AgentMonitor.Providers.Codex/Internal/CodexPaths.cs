namespace AgentMonitor.Providers.Codex.Internal;

/// <summary>Resolves the Codex CLI's on-disk locations. Isolated so a layout
/// change is a one-file fix.</summary>
internal sealed class CodexPaths
{
    public string Root { get; }
    public string SessionsDir => Path.Combine(Root, "sessions");

    /// <summary>The Codex desktop app's thread registry database.</summary>
    public string StateDbPath => Path.Combine(Root, "state_5.sqlite");

    public CodexPaths(string? rootOverride = null)
    {
        Root = rootOverride
            ?? Environment.GetEnvironmentVariable("CODEX_HOME")
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex");
    }

    public bool Exists => Directory.Exists(Root);
}
