using System.Text.Json;
using AgentMonitor.Core.Sessions;
using AgentMonitor.Providers.ClaudeCode.Internal;
using Xunit;

public class HookStatusInterpreterTests : IDisposable
{
    private const string Sid = "11111111-2222-3333-4444-555555555555";
    private const string Cwd = @"D:\Demo";

    private readonly string _root;
    private readonly ClaudePaths _paths;
    private readonly string _transcript;

    public HookStatusInterpreterTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "amhi-" + Guid.NewGuid().ToString("N")[..8]);
        var slug = ClaudePaths.SlugForCwd(Cwd);
        Directory.CreateDirectory(Path.Combine(_root, "projects", slug));
        Directory.CreateDirectory(Path.Combine(_root, "agent-monitor", "status"));
        _transcript = Path.Combine(_root, "projects", slug, Sid + ".jsonl");
        File.WriteAllText(_transcript, "{\"type\":\"assistant\",\"message\":{\"stop_reason\":\"tool_use\"}}\n");
        _paths = new ClaudePaths(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private static SessionRecord Record(string? tempo = null)
        => new() { Pid = 1, SessionId = Sid, Cwd = Cwd, Tempo = tempo };

    private void WriteMarker(string evt, DateTimeOffset ts, string? message = null)
        => File.WriteAllText(_paths.HookStatusFile(Sid),
            JsonSerializer.Serialize(new { sessionId = Sid, @event = evt, message, timestamp = ts.ToString("o") }));

    private void SetTranscript(DateTime utc) => File.SetLastWriteTimeUtc(_transcript, utc);

    private HookStatusInterpreter Sut() => new(_paths);

    [Fact]
    public void Daemon_tempo_blocked_wins_over_recent_transcript()
    {
        SetTranscript(DateTime.UtcNow);
        Assert.Equal(SessionStatus.AwaitingInput, Sut().Interpret(Record(tempo: "blocked")).Status);
    }

    [Fact]
    public void No_marker_falls_back_to_heuristic()
    {
        SetTranscript(DateTime.UtcNow.AddMinutes(-2)); // stale tool_use => idle awaiting
        Assert.Equal(SessionStatus.AwaitingInput, Sut().Interpret(Record()).Status);
    }

    [Fact]
    public void Stop_marker_reads_as_awaiting_finished()
    {
        SetTranscript(DateTime.UtcNow.AddMinutes(-2));
        WriteMarker("Stop", DateTimeOffset.UtcNow);
        var (status, detail, _) = Sut().Interpret(Record());
        Assert.Equal(SessionStatus.AwaitingInput, status);
        Assert.Equal("finished", detail);
    }

    [Fact]
    public void UserPromptSubmit_marker_reads_as_working()
    {
        SetTranscript(DateTime.UtcNow.AddMinutes(-2));
        WriteMarker("UserPromptSubmit", DateTimeOffset.UtcNow);
        Assert.Equal(SessionStatus.Working, Sut().Interpret(Record()).Status);
    }

    [Fact]
    public void Notification_marker_reads_as_awaiting_with_message()
    {
        SetTranscript(DateTime.UtcNow.AddMinutes(-2));
        WriteMarker("Notification", DateTimeOffset.UtcNow, "needs permission");
        var (status, detail, _) = Sut().Interpret(Record());
        Assert.Equal(SessionStatus.AwaitingInput, status);
        Assert.Equal("needs permission", detail);
    }

    [Fact]
    public void Stop_then_activity_reads_as_working_resumed()
    {
        WriteMarker("Stop", DateTimeOffset.UtcNow.AddSeconds(-30));
        SetTranscript(DateTime.UtcNow); // transcript newer than the marker => resumed
        Assert.Equal(SessionStatus.Working, Sut().Interpret(Record()).Status);
    }
}
