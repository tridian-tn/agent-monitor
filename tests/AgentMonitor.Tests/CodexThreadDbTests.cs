using AgentMonitor.Providers.Codex.Internal;
using Microsoft.Data.Sqlite;
using Xunit;

public class CodexThreadDbTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 6, 26, 18, 0, 0, TimeSpan.Zero);

    private readonly string _root;

    public CodexThreadDbTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "amtdb-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
        SeedDb(Path.Combine(_root, "state_5.sqlite"));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, true); } catch { }
    }

    private static void SeedDb(string path)
    {
        using var con = new SqliteConnection($"Data Source={path}");
        con.Open();
        using (var cmd = con.CreateCommand())
        {
            cmd.CommandText =
                "CREATE TABLE threads(id TEXT, rollout_path TEXT, cwd TEXT, title TEXT, " +
                "source TEXT, archived INTEGER, recency_at_ms INTEGER)";
            cmd.ExecuteNonQuery();
        }

        long recent = Now.AddMinutes(-5).ToUnixTimeMilliseconds();
        long old = Now.AddHours(-3).ToUnixTimeMilliseconds();

        void Insert(string id, string cwd, string title, int archived, long recency)
        {
            using var cmd = con.CreateCommand();
            cmd.CommandText =
                "INSERT INTO threads VALUES ($id, $rp, $cwd, $title, 'vscode', $arch, $rec)";
            cmd.Parameters.AddWithValue("$id", id);
            cmd.Parameters.AddWithValue("$rp", $@"D:\sessions\{id}.jsonl");
            cmd.Parameters.AddWithValue("$cwd", cwd);
            cmd.Parameters.AddWithValue("$title", title);
            cmd.Parameters.AddWithValue("$arch", archived);
            cmd.Parameters.AddWithValue("$rec", recency);
            cmd.ExecuteNonQuery();
        }

        Insert("keep", @"\\?\D:\proj", "My Title", archived: 0, recency: recent);
        Insert("too-old", @"\\?\D:\old", "Old", archived: 0, recency: old);
        Insert("archived", @"\\?\D:\arc", "Archived", archived: 1, recency: recent);
    }

    [Fact]
    public void Returns_only_recent_unarchived_with_clean_cwd_and_title()
    {
        var scanner = new ThreadDbScanner(new CodexPaths(_root));
        var metas = scanner.TryRecentSessions(TimeSpan.FromMinutes(15), Now);

        Assert.NotNull(metas);
        Assert.Single(metas);
        Assert.Equal("keep", metas![0].SessionId);
        Assert.Equal(@"D:\proj", metas[0].Cwd);   // \\?\ prefix stripped
        Assert.Equal("My Title", metas[0].Title);
    }

    [Fact]
    public void Missing_db_returns_null_so_provider_falls_back()
    {
        var scanner = new ThreadDbScanner(new CodexPaths(Path.Combine(_root, "nonexistent")));
        Assert.Null(scanner.TryRecentSessions(TimeSpan.FromMinutes(15), Now));
    }
}
