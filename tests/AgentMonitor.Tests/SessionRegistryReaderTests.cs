using AgentMonitor.Providers.ClaudeCode.Internal;
using Xunit;

public class SessionRegistryReaderTests : IDisposable
{
    private readonly string _root;

    public SessionRegistryReaderTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "amsr-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(Path.Combine(_root, "sessions"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private void Write(string name, string content)
        => File.WriteAllText(Path.Combine(_root, "sessions", name), content);

    [Fact]
    public void Reads_valid_records_and_skips_malformed_or_incomplete()
    {
        Write("1.json", """{"pid":1234,"sessionId":"abc","cwd":"D:\\proj","kind":"interactive","entrypoint":"cli"}""");
        Write("2.json", "{ this is not valid json");            // malformed -> skipped
        Write("3.json", """{"pid":0,"sessionId":"zero"}""");     // pid 0 -> skipped
        Write("4.json", """{"pid":9,"sessionId":""}""");          // empty id -> skipped
        Write("note.txt", "ignored");                              // not *.json

        var reader = new SessionRegistryReader(new ClaudePaths(_root));
        var records = reader.Read();

        Assert.Single(records);
        Assert.Equal(1234, records[0].Pid);
        Assert.Equal("abc", records[0].SessionId);
        Assert.Equal("cli", records[0].Entrypoint);
    }

    [Fact]
    public void Missing_sessions_dir_returns_empty()
    {
        var reader = new SessionRegistryReader(new ClaudePaths(Path.Combine(_root, "nonexistent")));
        Assert.Empty(reader.Read());
    }
}
