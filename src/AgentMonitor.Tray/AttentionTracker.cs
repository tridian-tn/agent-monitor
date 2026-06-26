using AgentMonitor.Core.Sessions;
using AgentMonitor.Core.Status;

namespace AgentMonitor.Tray;

/// <summary>
/// Turns a set of sessions into a tray colour by how much they want you:
/// <list type="bullet">
///   <item><b>Green</b> — at least one session became ready recently and you
///   haven't looked yet ("needs you now").</item>
///   <item><b>Amber</b> — something is working, nothing fresh waiting.</item>
///   <item><b>Grey</b> — nothing wants you (idle, already-seen, or not running).</item>
/// </list>
/// A waiting session stops counting as "fresh" once you look at it (its window
/// shares the foreground lineage), when it resumes/dies, or after a timeout.
/// </summary>
internal sealed class AttentionTracker
{
    private readonly TimeSpan _freshWindow;

    // sessionKey -> the AwaitingSince timestamp we've already acknowledged.
    private readonly Dictionary<string, DateTimeOffset> _seen = new();

    public AttentionTracker(TimeSpan? freshWindow = null)
    {
        _freshWindow = freshWindow ?? TimeSpan.FromMinutes(5);
    }

    public (TrayColor Color, IReadOnlyList<AgentSession> NeedsYou) Evaluate(
        IReadOnlyList<AgentSession> sessions,
        DateTimeOffset now,
        Func<int?> foregroundPid,
        Func<Dictionary<int, int>> parentMap)
    {
        var awaiting = sessions.Where(s => s.Status == SessionStatus.AwaitingInput).ToList();

        // Forget acknowledgements for sessions that are no longer awaiting, so a
        // later finish is treated as a brand-new ping.
        var awaitingKeys = awaiting.Select(Key).ToHashSet();
        foreach (var stale in _seen.Keys.Where(k => !awaitingKeys.Contains(k)).ToList())
            _seen.Remove(stale);

        // Only inspect the foreground when there's something a glance could clear.
        bool anyCandidate = awaiting.Any(s => IsFreshAndUnseen(s, now));
        int? fg = anyCandidate ? foregroundPid() : null;
        Dictionary<int, int>? parents = fg is not null ? parentMap() : null;

        // Focus only counts as "I looked at this one" when the focused window
        // uniquely identifies a session. If it hosts several (Claude Desktop with
        // multiple tabs, or a multi-tab terminal), we can't tell which tab you're
        // on, so we don't acknowledge by focus — a background finish still pings.
        bool focusIsPrecise = fg is int fgPid && parents is not null
            && sessions.Count(s => s.ProcessId is int p
                && ForegroundWindow.ShareLineage(p, fgPid, parents)) == 1;

        var needsYou = new List<AgentSession>();
        foreach (var session in awaiting)
        {
            var key = Key(session);
            var since = session.LastActivity ?? now;

            if (focusIsPrecise && fg is int focusPid && session.ProcessId is int sessionPid
                && parents is not null
                && ForegroundWindow.ShareLineage(sessionPid, focusPid, parents))
            {
                _seen[key] = since; // you're looking at this specific session
            }

            if (IsFreshAndUnseen(session, now))
                needsYou.Add(session);
        }

        var color = needsYou.Count > 0
            ? TrayColor.Green
            : sessions.Any(s => s.Status == SessionStatus.Working)
                ? TrayColor.Amber
                : TrayColor.Grey;

        return (color, needsYou);
    }

    private bool IsFreshAndUnseen(AgentSession session, DateTimeOffset now)
    {
        var since = session.LastActivity ?? now;
        bool fresh = now - since < _freshWindow;
        bool seen = _seen.TryGetValue(Key(session), out var acked) && acked >= since;
        return fresh && !seen;
    }

    private static string Key(AgentSession s) => $"{s.ProviderId}/{s.SessionId}";
}
