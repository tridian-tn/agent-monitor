using AgentMonitor.Core.Sessions;

namespace AgentMonitor.Core.Status;

/// <summary>Maps a set of sessions to a single traffic-light colour.</summary>
public interface IStatusPolicy
{
    /// <summary>Human-readable description, shown in the policy menu.</summary>
    string Name { get; }

    TrayColor Evaluate(IReadOnlyList<AgentSession> sessions);
}
