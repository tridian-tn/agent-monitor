using AgentMonitor.Core.Sessions;
using AgentMonitor.Providers.Codex.Internal;

namespace AgentMonitor.Providers.Codex;

/// <summary>
/// Surfaces Codex CLI sessions from its rollout logs under <c>~/.codex/sessions</c>.
/// Codex has no per-session PID registry, so "live" is approximated by rollout
/// recency while a Codex process is running. Status comes from the rollout's
/// turn-lifecycle events (<c>task_started</c> / <c>task_complete</c> / approval
/// requests).
///
/// The Electron/sqlite Codex desktop app is intentionally out of scope — its state
/// lives in opaque local databases, the same call made for the Claude desktop app.
/// </summary>
public sealed class CodexProvider : ISessionProvider
{
    private readonly CodexPaths _paths;
    private readonly ThreadDbScanner _dbScanner;
    private readonly RolloutScanner _fileScanner;
    private readonly RolloutStatusReader _status = new();
    private readonly TimeSpan _recencyWindow;
    private readonly TimeSpan _workingWindow;

    public CodexProvider(string? rootOverride = null, TimeSpan? recencyWindow = null, TimeSpan? workingWindow = null)
    {
        _paths = new CodexPaths(rootOverride);
        _dbScanner = new ThreadDbScanner(_paths);
        _fileScanner = new RolloutScanner(_paths);
        _recencyWindow = recencyWindow ?? TimeSpan.FromMinutes(15);
        _workingWindow = workingWindow ?? TimeSpan.FromSeconds(6);
    }

    public string Id => "codex";
    public string DisplayName => "Codex";
    public bool IsInstalled => _paths.Exists;

    public IReadOnlyList<AgentSession> GetSessions()
    {
        if (!_paths.Exists || !CodexProcess.IsRunning())
            return Array.Empty<AgentSession>();

        var now = DateTimeOffset.UtcNow;
        // Prefer the desktop app's thread registry (richer titles, archived filter);
        // fall back to scanning rollout files for a pure-CLI install.
        var metas = _dbScanner.TryRecentSessions(_recencyWindow, now)
            ?? _fileScanner.RecentSessions(_recencyWindow, now);

        var sessions = new List<AgentSession>();
        foreach (var meta in metas)
        {
            var (status, detail, lastActivity) = _status.Read(meta.Path, _workingWindow);
            sessions.Add(new AgentSession
            {
                ProviderId = Id,
                SessionId = meta.SessionId,
                Title = TitleFor(meta),
                WorkingDirectory = meta.Cwd,
                Status = status,
                Detail = detail,
                LastActivity = lastActivity,
                ProcessId = null, // no rollout-to-PID mapping exists
                Origin = meta.Originator,
            });
        }

        return sessions;
    }

    private const int MaxTitle = 48;

    private static string TitleFor(RolloutMeta meta)
    {
        if (!string.IsNullOrWhiteSpace(meta.Title))
            return Shorten(meta.Title.Trim());

        if (!string.IsNullOrEmpty(meta.Cwd))
        {
            var name = Path.GetFileName(meta.Cwd.TrimEnd('\\', '/'));
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        return meta.SessionId.Length >= 8 ? meta.SessionId[..8] : meta.SessionId;
    }

    private static string Shorten(string title)
    {
        // Desktop "Companion Task" threads use the whole first message as the title.
        var oneLine = title.ReplaceLineEndings(" ");
        return oneLine.Length <= MaxTitle ? oneLine : oneLine[..MaxTitle].TrimEnd() + "…";
    }
}
