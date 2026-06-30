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
    private readonly TimeSpan _activityWindow;

    public StatusAggregator(IReadOnlyList<ISessionProvider> providers, TimeSpan? activityWindow = null)
    {
        _providers = providers;
        _activityWindow = activityWindow ?? TimeSpan.FromMinutes(30);
    }

    public AggregateStatus Compute() => Compute(DateTimeOffset.UtcNow);

    public AggregateStatus Compute(DateTimeOffset now)
    {
        var sessions = new List<AgentSession>();
        foreach (var provider in _providers)
        {
            try
            {
                foreach (var session in provider.GetSessions())
                {
                    if (IsRecentlyActive(session, now))
                        sessions.Add(session);
                }
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

    /// <summary>
    /// The tray list is for sessions that are doing something now or did recently. A
    /// session earns a place by working/erroring, or by having a real status update
    /// within the activity window. An old session whose process is still alive — e.g.
    /// one you just reopened, whose last turn finished hours ago — is dropped. A
    /// missing timestamp means the status came from an explicit live signal (a
    /// daemon-reported state) with no clock to age out, so we keep it.
    /// </summary>
    private bool IsRecentlyActive(AgentSession session, DateTimeOffset now)
    {
        if (session.Status is SessionStatus.Working or SessionStatus.Error)
            return true;
        if (session.LastActivity is not DateTimeOffset last)
            return true;
        return now - last <= _activityWindow;
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
