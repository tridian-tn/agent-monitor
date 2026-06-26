using AgentMonitor.Core.Sessions;

namespace AgentMonitor.Providers.ClaudeCode.Internal;

/// <summary>
/// High-fidelity interpreter that layers Claude Code hook events over transcript
/// recency:
/// <list type="number">
///   <item>Daemon-backed sessions: the explicit <c>tempo</c> wins (via the heuristic).</item>
///   <item>If a hook marker exists, use the event — but if the transcript has been
///   written <em>after</em> the marker, the session has resumed work, so report
///   Working (this catches the gap after a permission prompt is approved).</item>
///   <item>No hook marker (hooks not installed, or none fired yet): fall back to the
///   transcript heuristic.</item>
/// </list>
/// </summary>
internal sealed class HookStatusInterpreter : ISessionStatusInterpreter
{
    private static readonly TimeSpan ResumeTolerance = TimeSpan.FromSeconds(2);

    private readonly ClaudePaths _paths;
    private readonly HookStatusStore _hooks;
    private readonly HeuristicStatusInterpreter _fallback;

    public HookStatusInterpreter(
        ClaudePaths paths, TimeSpan? workingWindow = null, TimeSpan? idleThreshold = null)
    {
        _paths = paths;
        _hooks = new HookStatusStore(paths);
        _fallback = new HeuristicStatusInterpreter(paths, workingWindow, idleThreshold);
    }

    public (SessionStatus Status, string? Detail, DateTimeOffset? LastActivity) Interpret(
        SessionRecord record)
    {
        // Daemon-backed sessions carry explicit status; let the heuristic handle them.
        if (record.Tempo is "active" or "idle" or "blocked")
            return _fallback.Interpret(record);

        var hook = _hooks.TryRead(record.SessionId);
        if (hook?.Event is null)
            return _fallback.Interpret(record);

        var lastActivity = hook.Timestamp;

        // UserPromptSubmit: a turn has started — definitely working.
        if (hook.Event == "UserPromptSubmit")
            return (SessionStatus.Working, null, lastActivity);

        // SubagentStop fires for a subagent finishing; the main agent continues.
        if (hook.Event == "SubagentStop")
            return (SessionStatus.Working, null, lastActivity);

        // Stop / Notification mean "awaiting you" — unless the transcript grew after
        // the event, in which case the session resumed (e.g. an approved tool ran).
        if (HasResumedSince(record, hook.Timestamp))
            return (SessionStatus.Working, null, DateTimeOffset.UtcNow);

        return hook.Event switch
        {
            "Stop" => (SessionStatus.AwaitingInput, "finished", lastActivity),
            "Notification" => (SessionStatus.AwaitingInput, hook.Message ?? "needs attention", lastActivity),
            _ => _fallback.Interpret(record),
        };
    }

    private bool HasResumedSince(SessionRecord record, DateTimeOffset? eventTime)
    {
        if (eventTime is null)
            return false;

        var path = _paths.FindTranscript(record.SessionId, record.Cwd);
        if (path is null)
            return false;

        try
        {
            var lastWrite = File.GetLastWriteTimeUtc(path);
            return lastWrite > eventTime.Value + ResumeTolerance;
        }
        catch
        {
            return false;
        }
    }
}
