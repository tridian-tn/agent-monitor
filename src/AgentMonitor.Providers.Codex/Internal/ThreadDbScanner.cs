using Microsoft.Data.Sqlite;

namespace AgentMonitor.Providers.Codex.Internal;

/// <summary>
/// Reads the Codex desktop app's <c>state_5.sqlite</c> <c>threads</c> registry —
/// a richer discovery source than scanning the filesystem (real titles, an
/// archived flag, and a recency timestamp). Status still comes from the rollout
/// file each thread points at, so this only replaces discovery.
///
/// The DB belongs to a running app, so we read a copy (with its WAL) rather than
/// the live file. Returns null when the DB is absent or unreadable, letting the
/// provider fall back to the filesystem scanner.
/// </summary>
internal sealed class ThreadDbScanner
{
    private readonly CodexPaths _paths;

    public ThreadDbScanner(CodexPaths paths) => _paths = paths;

    public IReadOnlyList<RolloutMeta>? TryRecentSessions(TimeSpan window, DateTimeOffset now)
    {
        if (!File.Exists(_paths.StateDbPath))
            return null;

        string tempDb = Path.Combine(
            Path.GetTempPath(), "agentmon-codexdb-" + Guid.NewGuid().ToString("N")[..8] + ".sqlite");

        try
        {
            CopySnapshot(_paths.StateDbPath, tempDb);
            return Query(tempDb, (now - window).ToUnixTimeMilliseconds());
        }
        catch
        {
            return null; // unreadable / locked / schema drift -> fall back
        }
        finally
        {
            foreach (var ext in new[] { "", "-wal", "-shm" })
                TryDelete(tempDb + ext);
        }
    }

    private static List<RolloutMeta> Query(string dbPath, long cutoffMs)
    {
        var result = new List<RolloutMeta>();

        using var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate, // our throwaway copy; lets WAL checkpoint
                Pooling = false,                       // so the file unlocks for deletion
            }.ToString());
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, rollout_path, cwd, title, source
            FROM threads
            WHERE archived = 0
              AND rollout_path IS NOT NULL
              AND recency_at_ms >= $cutoff
            ORDER BY recency_at_ms DESC
            """;
        command.Parameters.AddWithValue("$cutoff", cutoffMs);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetString(0);
            var rolloutPath = reader.GetString(1);
            var cwd = NormalizeCwd(reader.IsDBNull(2) ? null : reader.GetString(2));
            var title = reader.IsDBNull(3) ? null : reader.GetString(3);
            var source = reader.IsDBNull(4) ? null : reader.GetString(4);

            result.Add(new RolloutMeta(rolloutPath, id, cwd, source, title));
        }

        return result;
    }

    private static void CopySnapshot(string source, string destination)
    {
        foreach (var ext in new[] { "", "-wal", "-shm" })
        {
            var from = source + ext;
            if (File.Exists(from))
                File.Copy(from, destination + ext, overwrite: true);
        }
    }

    /// <summary>Strips the Windows extended-length prefix (<c>\\?\</c>) the app stores.</summary>
    private static string? NormalizeCwd(string? cwd)
        => cwd is not null && cwd.StartsWith(@"\\?\", StringComparison.Ordinal) ? cwd[4..] : cwd;

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}
