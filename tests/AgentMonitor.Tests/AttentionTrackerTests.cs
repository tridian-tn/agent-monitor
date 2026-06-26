using AgentMonitor.Core.Sessions;
using AgentMonitor.Core.Status;
using AgentMonitor.Tray;
using Xunit;

public class AttentionTrackerTests
{
    private static readonly TimeSpan Fresh = TimeSpan.FromMinutes(5);
    private static readonly DateTimeOffset Now = new(2026, 6, 26, 12, 0, 0, TimeSpan.Zero);

    private static AgentSession Awaiting(DateTimeOffset since, int pid = 100, string id = "s1")
        => new()
        {
            ProviderId = "claude-code",
            SessionId = id,
            Title = id,
            Status = SessionStatus.AwaitingInput,
            LastActivity = since,
            ProcessId = pid,
        };

    private static AgentSession Working(string id = "w1")
        => new() { ProviderId = "claude-code", SessionId = id, Title = id, Status = SessionStatus.Working };

    // No window is focused on the agent.
    private static readonly Func<int?> NoFocus = () => 999;
    private static readonly Func<Dictionary<int, int>> NoMap = () => new();

    [Fact]
    public void Recent_unseen_awaiting_is_green_and_needs_you()
    {
        var tracker = new AttentionTracker(Fresh);
        var (color, needsYou) = tracker.Evaluate(
            new[] { Awaiting(Now.AddSeconds(-30)) }, Now, NoFocus, NoMap);

        Assert.Equal(TrayColor.Green, color);
        Assert.Single(needsYou);
    }

    [Fact]
    public void Working_only_is_amber()
    {
        var tracker = new AttentionTracker(Fresh);
        var (color, needsYou) = tracker.Evaluate(new[] { Working() }, Now, NoFocus, NoMap);

        Assert.Equal(TrayColor.Amber, color);
        Assert.Empty(needsYou);
    }

    [Fact]
    public void Nothing_is_grey()
    {
        var tracker = new AttentionTracker(Fresh);
        var (color, _) = tracker.Evaluate(Array.Empty<AgentSession>(), Now, NoFocus, NoMap);
        Assert.Equal(TrayColor.Grey, color);
    }

    [Fact]
    public void Stale_awaiting_past_timeout_is_grey()
    {
        var tracker = new AttentionTracker(Fresh);
        var (color, needsYou) = tracker.Evaluate(
            new[] { Awaiting(Now.AddMinutes(-10)) }, Now, NoFocus, NoMap);

        Assert.Equal(TrayColor.Grey, color);
        Assert.Empty(needsYou);
    }

    [Fact]
    public void Focusing_the_session_window_clears_green()
    {
        var tracker = new AttentionTracker(Fresh);
        var session = Awaiting(Now.AddSeconds(-30), pid: 100);

        // Foreground pid 100 == the session's pid => same lineage => "you looked".
        var (color, needsYou) = tracker.Evaluate(new[] { session }, Now, () => 100, NoMap);

        Assert.Equal(TrayColor.Grey, color);
        Assert.Empty(needsYou);
    }

    [Fact]
    public void Focus_clears_green_only_when_it_uniquely_identifies_the_session()
    {
        // Session reachable from the foreground only via a shared ancestor (e.g. a
        // terminal hosting just this one claude). Unique -> focus acknowledges.
        var tracker = new AttentionTracker(Fresh);
        var session = Awaiting(Now.AddSeconds(-30), pid: 101);
        var map = new Dictionary<int, int> { [101] = 200 }; // 101's parent is window 200

        var (color, _) = tracker.Evaluate(new[] { session }, Now, () => 200, () => map);
        Assert.Equal(TrayColor.Grey, color);
    }

    [Fact]
    public void Background_tab_still_goes_green_when_app_hosts_several_sessions()
    {
        // Claude Desktop: one window (200) hosts two session processes. Focusing the
        // app can't say which tab you're on, so a background finish must still ping.
        var tracker = new AttentionTracker(Fresh);
        var awaiting = Awaiting(Now.AddSeconds(-30), pid: 101, id: "bg");
        var working = new AgentSession
        {
            ProviderId = "claude-code", SessionId = "fg", Title = "fg",
            Status = SessionStatus.Working, ProcessId = 102,
        };
        var map = new Dictionary<int, int> { [101] = 200, [102] = 200 };

        var (color, needsYou) = tracker.Evaluate(
            new[] { awaiting, working }, Now, () => 200, () => map);

        Assert.Equal(TrayColor.Green, color);
        Assert.Single(needsYou);
        Assert.Equal("bg", needsYou[0].SessionId);
    }

    [Fact]
    public void Once_seen_stays_grey_for_the_same_episode()
    {
        var tracker = new AttentionTracker(Fresh);
        var since = Now.AddSeconds(-30);

        tracker.Evaluate(new[] { Awaiting(since, pid: 100) }, Now, () => 100, NoMap); // seen
        var (color, _) = tracker.Evaluate(
            new[] { Awaiting(since, pid: 100) }, Now.AddSeconds(5), NoFocus, NoMap);

        Assert.Equal(TrayColor.Grey, color);
    }

    [Fact]
    public void A_new_finish_after_being_seen_goes_green_again()
    {
        var tracker = new AttentionTracker(Fresh);

        // Episode 1: seen.
        tracker.Evaluate(new[] { Awaiting(Now.AddSeconds(-30), pid: 100) }, Now, () => 100, NoMap);
        // It resumed (drops out of awaiting), then finished again later.
        tracker.Evaluate(new[] { Working("s1") }, Now.AddSeconds(60), NoFocus, NoMap);

        var laterFinish = Awaiting(Now.AddSeconds(115), pid: 100);
        var (color, needsYou) = tracker.Evaluate(
            new[] { laterFinish }, Now.AddSeconds(120), NoFocus, NoMap);

        Assert.Equal(TrayColor.Green, color);
        Assert.Single(needsYou);
    }
}
