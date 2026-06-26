using System.Text.Json;

namespace AgentMonitor.Providers.Codex.Internal;

/// <summary>Metadata parsed from a rollout file's <c>session_meta</c> header.</summary>
internal sealed record RolloutMeta(string Path, string SessionId, string? Cwd, string? Originator);

/// <summary>
/// Finds rollout files touched within a recency window (Codex's stand-in for a
/// live-session registry — there is no PID registry) and reads their headers.
/// </summary>
internal sealed class RolloutScanner
{
    private readonly CodexPaths _paths;

    public RolloutScanner(CodexPaths paths) => _paths = paths;

    public IReadOnlyList<RolloutMeta> RecentSessions(TimeSpan window, DateTimeOffset now)
    {
        if (!Directory.Exists(_paths.SessionsDir))
            return Array.Empty<RolloutMeta>();

        var cutoff = now - window;
        var result = new List<RolloutMeta>();

        foreach (var path in EnumerateRollouts())
        {
            DateTimeOffset mtime;
            try
            {
                mtime = File.GetLastWriteTimeUtc(path);
            }
            catch
            {
                continue;
            }

            if (mtime < cutoff)
                continue;

            var meta = ReadMeta(path);
            if (meta is not null)
                result.Add(meta);
        }

        return result;
    }

    private IEnumerable<string> EnumerateRollouts()
    {
        try
        {
            return Directory.EnumerateFiles(_paths.SessionsDir, "rollout-*.jsonl", SearchOption.AllDirectories);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static RolloutMeta? ReadMeta(string path)
    {
        try
        {
            string? first = File.ReadLines(path).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
            if (first is null)
                return null;

            using var doc = JsonDocument.Parse(first);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var t) || t.GetString() != "session_meta")
                return null;
            if (!root.TryGetProperty("payload", out var p) || p.ValueKind != JsonValueKind.Object)
                return null;

            string? sessionId = Get(p, "session_id") ?? Get(p, "id");
            if (string.IsNullOrEmpty(sessionId))
                return null;

            return new RolloutMeta(path, sessionId, Get(p, "cwd"), Get(p, "originator"));
        }
        catch
        {
            return null;
        }
    }

    private static string? Get(JsonElement obj, string name)
        => obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
