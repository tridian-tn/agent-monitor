using AgentMonitor.Core.Sessions;
using AgentMonitor.Core.Status;
using Xunit;

public class StatusAggregatorTests
{
    private sealed class FakeProvider(string id, Func<IReadOnlyList<AgentSession>> get) : ISessionProvider
    {
        public string Id => id;
        public string DisplayName => id;
        public bool IsInstalled => true;
        public IReadOnlyList<AgentSession> GetSessions() => get();
    }

    private static AgentSession Session(string provider, string id, SessionStatus status)
        => new() { ProviderId = provider, SessionId = id, Title = id, Status = status };

    private static AgentSession Session(
        string provider, string id, SessionStatus status, DateTimeOffset lastActivity)
        => new()
        {
            ProviderId = provider,
            SessionId = id,
            Title = id,
            Status = status,
            LastActivity = lastActivity,
        };

    [Fact]
    public void Merges_sessions_from_all_providers()
    {
        var aggregator = new StatusAggregator(new ISessionProvider[]
        {
            new FakeProvider("a", () => new[] { Session("a", "1", SessionStatus.Working) }),
            new FakeProvider("b", () => new[]
            {
                Session("b", "2", SessionStatus.AwaitingInput),
                Session("b", "3", SessionStatus.Working),
            }),
        });

        var result = aggregator.Compute();

        Assert.Equal(3, result.Sessions.Count);
        Assert.Equal(2, result.WorkingCount);
        Assert.Equal(1, result.AwaitingCount);
    }

    [Fact]
    public void A_throwing_provider_does_not_break_the_others()
    {
        var aggregator = new StatusAggregator(new ISessionProvider[]
        {
            new FakeProvider("bad", () => throw new InvalidOperationException("boom")),
            new FakeProvider("good", () => new[] { Session("good", "1", SessionStatus.Working) }),
        });

        var result = aggregator.Compute();

        Assert.Single(result.Sessions);
        Assert.Equal("good", result.Sessions[0].ProviderId);
    }

    [Fact]
    public void Drops_awaiting_sessions_whose_last_activity_is_stale()
    {
        // A reopened old session: alive, finished hours ago. It must not appear.
        var now = DateTimeOffset.UtcNow;
        var aggregator = new StatusAggregator(new ISessionProvider[]
        {
            new FakeProvider("a", () => new[]
            {
                Session("a", "fresh", SessionStatus.AwaitingInput, now.AddMinutes(-5)),
                Session("a", "stale", SessionStatus.AwaitingInput, now.AddHours(-3)),
            }),
        });

        var result = aggregator.Compute(now);

        Assert.Single(result.Sessions);
        Assert.Equal("fresh", result.Sessions[0].SessionId);
    }

    [Fact]
    public void Keeps_working_sessions_regardless_of_timestamp()
    {
        // Working is a live signal; an old write time shouldn't age it out.
        var now = DateTimeOffset.UtcNow;
        var aggregator = new StatusAggregator(new ISessionProvider[]
        {
            new FakeProvider("a", () => new[]
            {
                Session("a", "w", SessionStatus.Working, now.AddHours(-3)),
            }),
        });

        Assert.Single(aggregator.Compute(now).Sessions);
    }

    [Fact]
    public void Keeps_sessions_without_a_timestamp()
    {
        // Explicit live status (e.g. daemon-reported) with no clock to age out.
        var now = DateTimeOffset.UtcNow;
        var aggregator = new StatusAggregator(new ISessionProvider[]
        {
            new FakeProvider("a", () => new[] { Session("a", "d", SessionStatus.AwaitingInput) }),
        });

        Assert.Single(aggregator.Compute(now).Sessions);
    }

    [Fact]
    public void No_sessions_yields_the_empty_summary()
    {
        var aggregator = new StatusAggregator(Array.Empty<ISessionProvider>());

        var result = aggregator.Compute();

        Assert.Empty(result.Sessions);
        Assert.Equal("No active sessions", result.Summary);
    }
}
