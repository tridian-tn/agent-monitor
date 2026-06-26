using AgentMonitor.Core.Sessions;
using AgentMonitor.Providers.ClaudeCode.Internal;
using Xunit;

public class TranscriptStatusReaderTests : IDisposable
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(30);

    private readonly string _dir;
    private readonly TranscriptStatusReader _reader = new();

    public TranscriptStatusReaderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "amtr-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private string Write(string content, DateTime mtimeUtc)
    {
        var path = Path.Combine(_dir, "t.jsonl");
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, mtimeUtc);
        return path;
    }

    [Fact]
    public void EndTurn_with_trailing_attachment_reads_as_finished()
    {
        // The last *line* is an attachment; the reader must scan back to the
        // assistant record's stop_reason.
        var path = Write(
            "{\"type\":\"assistant\",\"message\":{\"stop_reason\":\"end_turn\"}}\n{\"type\":\"attachment\"}\n",
            DateTime.UtcNow.AddMinutes(-2));

        var (status, detail, _) = _reader.Read(path, Window, Idle);

        Assert.Equal(SessionStatus.AwaitingInput, status);
        Assert.Equal("finished", detail);
    }

    [Fact]
    public void Recent_tooluse_reads_as_working()
    {
        var path = Write(
            "{\"type\":\"assistant\",\"message\":{\"stop_reason\":\"tool_use\"}}\n",
            DateTime.UtcNow);

        Assert.Equal(SessionStatus.Working, _reader.Read(path, Window, Idle).Status);
    }

    [Fact]
    public void Stale_tooluse_reads_as_awaiting_idle()
    {
        var path = Write(
            "{\"type\":\"assistant\",\"message\":{\"stop_reason\":\"tool_use\"}}\n",
            DateTime.UtcNow.AddMinutes(-2));

        var (status, detail, _) = _reader.Read(path, Window, Idle);

        Assert.Equal(SessionStatus.AwaitingInput, status);
        Assert.Equal("idle", detail);
    }

    [Fact]
    public void Empty_transcript_does_not_crash_or_report_finished()
    {
        var path = Write("", DateTime.UtcNow.AddMinutes(-2));
        var (_, detail, _) = _reader.Read(path, Window, Idle);
        Assert.NotEqual("finished", detail);
    }

    [Fact]
    public void Transcript_with_no_assistant_record_is_not_reported_finished()
    {
        // Only a user record, at rest — must never be mistaken for a completed turn.
        var path = Write(
            "{\"type\":\"user\",\"message\":{\"role\":\"user\"}}\n",
            DateTime.UtcNow.AddMinutes(-2));

        var (_, detail, _) = _reader.Read(path, Window, Idle);
        Assert.NotEqual("finished", detail);
    }
}
