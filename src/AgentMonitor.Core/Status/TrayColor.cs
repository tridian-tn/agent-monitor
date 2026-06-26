namespace AgentMonitor.Core.Status;

/// <summary>The states the tray icon can display, ordered by how much they want you.</summary>
public enum TrayColor
{
    /// <summary>Nothing wants you: idle, already-seen, or not running. The resting state.</summary>
    Grey,

    /// <summary>At least one session is working; nothing is freshly waiting on you.</summary>
    Amber,

    /// <summary>A session recently became ready and you haven't looked yet. The ping.</summary>
    Green,

    /// <summary>Reserved: nothing running (only used if "not running" is shown distinctly).</summary>
    Red,
}
