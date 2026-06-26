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
    public void No_sessions_yields_the_empty_summary()
    {
        var aggregator = new StatusAggregator(Array.Empty<ISessionProvider>());

        var result = aggregator.Compute();

        Assert.Empty(result.Sessions);
        Assert.Equal("No active sessions", result.Summary);
    }
}
