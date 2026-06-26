using AgentMonitor.Core.Sessions;

namespace AgentMonitor.Core.Status;

/// <summary>The rolled-up state shown by the tray icon at a point in time.</summary>
public sealed record AggregateStatus
{
    public required TrayColor Color { get; init; }
    public required IReadOnlyList<AgentSession> Sessions { get; init; }
    public required string Summary { get; init; }

    public int WorkingCount => Sessions.Count(s => s.Status == SessionStatus.Working);
    public int AwaitingCount => Sessions.Count(s => s.Status == SessionStatus.AwaitingInput);
}
