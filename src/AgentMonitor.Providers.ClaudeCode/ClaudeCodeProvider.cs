using AgentMonitor.Core.Sessions;
using AgentMonitor.Providers.ClaudeCode.Internal;

namespace AgentMonitor.Providers.ClaudeCode;

/// <summary>
/// Surfaces Claude Code sessions — desktop and terminal alike — by reading the
/// <c>~/.claude</c> session registry and transcripts. The registry is written by
/// every session regardless of entrypoint, so this covers both the desktop app
/// (entrypoint "claude-desktop") and the CLI (entrypoint "cli").
/// </summary>
public sealed class ClaudeCodeProvider : ISessionProvider
{
    private readonly ClaudePaths _paths;
    private readonly SessionRegistryReader _registry;
    private readonly HookStatusStore _hooks;
    private readonly ISessionStatusInterpreter _interpreter;

    public ClaudeCodeProvider(string? rootOverride = null)
    {
        _paths = new ClaudePaths(rootOverride);
        _registry = new SessionRegistryReader(_paths);
        _hooks = new HookStatusStore(_paths);
        // The hook-aware interpreter falls back to the transcript heuristic when no
        // hook markers are present, so it is always safe to use.
        _interpreter = new HookStatusInterpreter(_paths);
    }

    public string Id => "claude-code";
    public string DisplayName => "Claude Code";
    public bool IsInstalled => _paths.Exists;

    /// <summary>True once the hooks have run at least once (precise mode active).</summary>
    public bool HooksActive => _hooks.IsActive;

    /// <summary>
    /// When false (default) only interactive sessions are surfaced; background and
    /// one-shot print jobs are ignored.
    /// </summary>
    public bool IncludeNonInteractive { get; set; }

    public IReadOnlyList<AgentSession> GetSessions()
    {
        var sessions = new List<AgentSession>();

        foreach (var record in _registry.Read())
        {
            if (!IncludeNonInteractive && !IsInteractive(record))
                continue;
            if (!ProcessLiveness.IsAlive(record))
                continue;

            var (status, detail, lastActivity) = _interpreter.Interpret(record);
            sessions.Add(new AgentSession
            {
                ProviderId = Id,
                SessionId = record.SessionId,
                Title = TitleFor(record),
                WorkingDirectory = record.Cwd,
                Status = status,
                Detail = detail,
                LastActivity = lastActivity,
                ProcessId = record.Pid,
                Origin = record.Entrypoint,
            });
        }

        return sessions;
    }

    private static bool IsInteractive(SessionRecord record)
        => string.IsNullOrEmpty(record.Kind) || record.Kind == "interactive";

    private static string TitleFor(SessionRecord record)
    {
        if (!string.IsNullOrEmpty(record.Cwd))
        {
            var name = Path.GetFileName(record.Cwd.TrimEnd('\\', '/'));
            if (!string.IsNullOrEmpty(name))
                return name;
        }

        return record.SessionId.Length >= 8 ? record.SessionId[..8] : record.SessionId;
    }
}
