namespace AgentMonitor.Core.Status;

/// <summary>The three traffic-light states the tray icon can display.</summary>
public enum TrayColor
{
    /// <summary>No live sessions / tool not running.</summary>
    Red,

    /// <summary>At least one session working and none awaiting the user.</summary>
    Amber,

    /// <summary>A session is awaiting the user, or nothing is currently working.</summary>
    Green,
}
