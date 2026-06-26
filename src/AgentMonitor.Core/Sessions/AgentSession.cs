namespace AgentMonitor.Core.Sessions;

/// <summary>A single agent session surfaced by a provider, in normalized form.</summary>
public sealed record AgentSession
{
    /// <summary>Id of the provider that surfaced this session, e.g. "claude-code".</summary>
    public required string ProviderId { get; init; }

    /// <summary>Provider-unique session identifier.</summary>
    public required string SessionId { get; init; }

    /// <summary>Human-friendly label, typically the working-directory name.</summary>
    public required string Title { get; init; }

    /// <summary>Absolute working directory, if known.</summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>Normalized activity state.</summary>
    public SessionStatus Status { get; init; } = SessionStatus.Unknown;

    /// <summary>Optional short detail, e.g. "awaiting approval" or "API error".</summary>
    public string? Detail { get; init; }

    /// <summary>Timestamp of the most recent observed activity, if known.</summary>
    public DateTimeOffset? LastActivity { get; init; }

    /// <summary>OS process id backing the session, if applicable.</summary>
    public int? ProcessId { get; init; }

    /// <summary>Provider-specific origin label, e.g. "cli" vs "claude-desktop".</summary>
    public string? Origin { get; init; }
}
