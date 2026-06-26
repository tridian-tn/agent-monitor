using AgentMonitor.Core.Sessions;

namespace AgentMonitor.Core.Status;

/// <summary>
/// Polls every provider and merges their sessions into one snapshot. The tray
/// colour is decided downstream (it needs UI-side state like focus and freshness),
/// so this type is purely about gathering sessions.
/// </summary>
public sealed class StatusAggregator
{
    private readonly IReadOnlyList<ISessionProvider> _providers;

    public StatusAggregator(IReadOnlyList<ISessionProvider> providers)
    {
        _providers = providers;
    }

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

        return new AggregateStatus
        {
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
