using AgentMonitor.Core.Sessions;

namespace AgentMonitor.Core.Status;

/// <summary>
/// Default policy. Green means "something wants you": at least one session has
/// finished its turn / is blocked on input, or nothing is currently working.
/// Amber means at least one session is working and none are awaiting you.
/// Red means there are no live sessions at all.
/// </summary>
public sealed class AwaitingYouPolicy : IStatusPolicy
{
    public string Name => "Green = a session is awaiting you";

    public TrayColor Evaluate(IReadOnlyList<AgentSession> sessions)
    {
        if (sessions.Count == 0)
            return TrayColor.Red;

        if (sessions.Any(s => s.Status == SessionStatus.AwaitingInput))
            return TrayColor.Green;

        if (sessions.Any(s => s.Status == SessionStatus.Working))
            return TrayColor.Amber;

        // All sessions idle/done — nothing pending.
        return TrayColor.Green;
    }
}
