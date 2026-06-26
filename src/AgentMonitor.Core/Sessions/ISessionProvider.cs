namespace AgentMonitor.Core.Sessions;

/// <summary>
/// A source of agent sessions for one LLM tool (Claude Code, Codex, ...).
/// Implementations isolate all tool-specific discovery and status parsing, and
/// expose only the normalized <see cref="AgentSession"/> model.
/// </summary>
public interface ISessionProvider
{
    /// <summary>Stable identifier, e.g. "claude-code".</summary>
    string Id { get; }

    /// <summary>Display name for menus, e.g. "Claude Code".</summary>
    string DisplayName { get; }

    /// <summary>Whether the underlying tool appears to be installed on this machine.</summary>
    bool IsInstalled { get; }

    /// <summary>
    /// Returns a current snapshot of live sessions. Must be cheap, non-blocking and
    /// resilient — the tray polls this on a timer, so it should never throw; return
    /// an empty list on failure instead.
    /// </summary>
    IReadOnlyList<AgentSession> GetSessions();
}
