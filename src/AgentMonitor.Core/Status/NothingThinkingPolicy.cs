using AgentMonitor.Core.Sessions;

namespace AgentMonitor.Core.Status;

/// <summary>
/// Alternative policy matching the original brief: Green = nothing is thinking,
/// Amber = at least one session thinking, Red = no live sessions.
/// </summary>
public sealed class NothingThinkingPolicy : IStatusPolicy
{
    public string Name => "Green = nothing thinking";

    public TrayColor Evaluate(IReadOnlyList<AgentSession> sessions)
    {
        if (sessions.Count == 0)
            return TrayColor.Red;

        return sessions.Any(s => s.Status == SessionStatus.Working)
            ? TrayColor.Amber
            : TrayColor.Green;
    }
}
