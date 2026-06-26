using AgentMonitor.Core.Sessions;
using AgentMonitor.Providers.Codex.Internal;
using Xunit;

public class CodexRolloutTests : IDisposable
{
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(6);

    private readonly string _dir;
    private readonly RolloutStatusReader _reader = new();

    public CodexRolloutTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "amcx-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_dir);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static string Event(string ptype)
        => "{\"type\":\"event_msg\",\"payload\":{\"type\":\"" + ptype + "\"}}";

    private const string AssistantMsg =
        "{\"type\":\"response_item\",\"payload\":{\"type\":\"message\",\"role\":\"assistant\"}}";

    private string Write(string content, DateTime mtimeUtc)
    {
        var path = Path.Combine(_dir, "rollout-x.jsonl");
        File.WriteAllText(path, content);
        File.SetLastWriteTimeUtc(path, mtimeUtc);
        return path;
    }

    [Fact]
    public void TaskComplete_reads_as_awaiting_finished()
    {
        // A trailing token_count after task_complete must be ignored.
        var path = Write(
            Event("task_started") + "\n" + AssistantMsg + "\n" + Event("task_complete") + "\n"
            + "{\"type\":\"event_msg\",\"payload\":{\"type\":\"token_count\"}}\n",
            DateTime.UtcNow.AddMinutes(-2));

        var (status, detail, _) = _reader.Read(path, Window);
        Assert.Equal(SessionStatus.AwaitingInput, status);
        Assert.Equal("finished", detail);
    }

    [Fact]
    public void TaskStarted_without_completion_reads_as_working()
    {
        var path = Write(Event("task_started") + "\n" + AssistantMsg + "\n",
            DateTime.UtcNow.AddMinutes(-2)); // stale, but turn still open

        Assert.Equal(SessionStatus.Working, _reader.Read(path, Window).Status);
    }

    [Fact]
    public void Approval_request_reads_as_awaiting()
    {
        var path = Write(Event("task_started") + "\n" + Event("exec_approval_request") + "\n",
            DateTime.UtcNow.AddMinutes(-2));

        var (status, detail, _) = _reader.Read(path, Window);
        Assert.Equal(SessionStatus.AwaitingInput, status);
        Assert.Equal("needs approval", detail);
    }

    [Fact]
    public void Recent_writes_read_as_working_even_without_lifecycle_event()
    {
        var path = Write(AssistantMsg + "\n", DateTime.UtcNow); // just written
        Assert.Equal(SessionStatus.Working, _reader.Read(path, Window).Status);
    }
}

public class CodexScannerTests : IDisposable
{
    private const string Sid = "019f03c3-8d19-7f63-be89-1611e9639e78";

    private readonly string _root;

    public CodexScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "amcxs-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_root, "sessions", "2026", "06", "26"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private string WriteRollout(string name, DateTime mtimeUtc, string cwd = @"D:\proj")
    {
        var path = Path.Combine(_root, "sessions", "2026", "06", "26", name);
        var meta = "{\"type\":\"session_meta\",\"payload\":{\"session_id\":\"" + Sid
            + "\",\"cwd\":\"" + cwd.Replace("\\", "\\\\") + "\",\"originator\":\"Codex CLI\"}}";
        File.WriteAllText(path, meta + "\n{\"type\":\"event_msg\",\"payload\":{\"type\":\"task_complete\"}}\n");
        File.SetLastWriteTimeUtc(path, mtimeUtc);
        return path;
    }

    [Fact]
    public void Only_rollouts_within_the_recency_window_are_returned()
    {
        var now = new DateTimeOffset(2026, 6, 26, 18, 0, 0, TimeSpan.Zero);
        WriteRollout("rollout-recent.jsonl", now.UtcDateTime.AddMinutes(-5));
        WriteRollout("rollout-old.jsonl", now.UtcDateTime.AddHours(-3));

        var scanner = new RolloutScanner(new CodexPaths(_root));
        var recent = scanner.RecentSessions(TimeSpan.FromMinutes(15), now);

        Assert.Single(recent);
        Assert.Equal(Sid, recent[0].SessionId);
        Assert.Equal("Codex CLI", recent[0].Originator);
    }
}
