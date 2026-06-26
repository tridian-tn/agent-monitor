using AgentMonitor.Core.Sessions;

namespace AgentMonitor.Providers.ClaudeCode.Internal;

/// <summary>
/// Default interpreter. Prefers the explicit daemon <c>tempo</c>/<c>needs</c>
/// fields when a record carries them; otherwise falls back to the transcript
/// heuristic for interactive sessions.
/// </summary>
internal sealed class HeuristicStatusInterpreter : ISessionStatusInterpreter
{
    private readonly ClaudePaths _paths;
    private readonly TranscriptStatusReader _transcript = new();
    private readonly TimeSpan _workingWindow;
    private readonly TimeSpan _idleThreshold;

    public HeuristicStatusInterpreter(
        ClaudePaths paths, TimeSpan? workingWindow = null, TimeSpan? idleThreshold = null)
    {
        _paths = paths;
        _workingWindow = workingWindow ?? TimeSpan.FromSeconds(6);
        _idleThreshold = idleThreshold ?? TimeSpan.FromSeconds(30);
    }

    public (SessionStatus Status, string? Detail, DateTimeOffset? LastActivity) Interpret(
        SessionRecord record)
    {
        // 1. High-fidelity path: daemon-backed sessions carry an explicit tempo.
        switch (record.Tempo)
        {
            case "active":
                return (SessionStatus.Working, record.Detail, null);
            case "blocked":
                return (SessionStatus.AwaitingInput, record.Needs ?? record.Detail ?? "blocked", null);
            case "idle":
                return (SessionStatus.AwaitingInput, record.Detail, null);
        }

        // 2. Transcript heuristic for interactive sessions.
        var path = _paths.FindTranscript(record.SessionId, record.Cwd);
        if (path is null)
            return (SessionStatus.Unknown, null, null);

        return _transcript.Read(path, _workingWindow, _idleThreshold);
    }
}
