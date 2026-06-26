using AgentMonitor.Core.Sessions;

namespace AgentMonitor.Core.Status;

/// <summary>
/// Polls every provider, merges their sessions into one list, and applies the
/// active <see cref="IStatusPolicy"/> to produce a single tray colour.
/// </summary>
public sealed class StatusAggregator
{
    private readonly IReadOnlyList<ISessionProvider> _providers;

    public StatusAggregator(IReadOnlyList<ISessionProvider> providers)
    {
        _providers = providers;
    }

    /// <summary>The active colour policy. Swappable at runtime.</summary>
    public IStatusPolicy Policy { get; set; } = new AwaitingYouPolicy();

    public AggregateStatus Compute()
    {
        var sessions = new List<AgentSession>();
        foreach (var provider in _providers)
        {
            try
            {
                sessions.AddRange(provider.GetSessions());
            }
            catch
            {
                // Providers are meant to be resilient; never let one break the tray.
            }
        }

        var color = Policy.Evaluate(sessions);
        return new AggregateStatus
        {
            Color = color,
            Sessions = sessions,
            Summary = BuildSummary(sessions),
        };
    }

    private static string BuildSummary(IReadOnlyList<AgentSession> sessions)
    {
        if (sessions.Count == 0)
            return "No active sessions";

        int working = sessions.Count(s => s.Status == SessionStatus.Working);
        int awaiting = sessions.Count(s => s.Status == SessionStatus.AwaitingInput);
        return $"{sessions.Count} session(s) — {working} working, {awaiting} awaiting you";
    }
}
