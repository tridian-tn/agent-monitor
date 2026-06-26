namespace AgentMonitor.Core.Sessions;

/// <summary>
/// Normalized, provider-agnostic activity state for a single agent session.
/// Every provider maps its tool-specific states onto this enum.
/// </summary>
public enum SessionStatus
{
    /// <summary>State could not be determined.</summary>
    Unknown,

    /// <summary>The agent is actively generating output or running tools.</summary>
    Working,

    /// <summary>The agent has finished its turn or is blocked waiting on the user.</summary>
    AwaitingInput,

    /// <summary>The session is alive but idle with nothing pending.</summary>
    Idle,

    /// <summary>The session ended in an error state.</summary>
    Error,
}
