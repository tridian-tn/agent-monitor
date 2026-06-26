using System.Text;
using System.Text.Json;
using AgentMonitor.Core.Sessions;

namespace AgentMonitor.Providers.Codex.Internal;

/// <summary>
/// Tails a Codex rollout <c>.jsonl</c> and infers status from the most recent
/// turn-lifecycle event plus write-recency. Codex emits explicit
/// <c>event_msg</c> markers, so the signal is clean:
/// <list type="bullet">
///   <item><c>task_complete</c> — the turn finished → awaiting you.</item>
///   <item>an <c>*approval*</c> request — Codex is blocked on you → awaiting you.</item>
///   <item><c>task_started</c> (no completion yet) or recent writes → working.</item>
/// </list>
/// </summary>
internal sealed class RolloutStatusReader
{
    private const int TailBytes = 64 * 1024;

    public (SessionStatus Status, string? Detail, DateTimeOffset? LastActivity) Read(
        string path, TimeSpan workingWindow)
    {
        DateTimeOffset lastWrite;
        try
        {
            lastWrite = File.GetLastWriteTimeUtc(path);
        }
        catch
        {
            return (SessionStatus.Unknown, null, null);
        }

        bool recentlyActive = DateTimeOffset.UtcNow - lastWrite <= workingWindow;
        string? lastEvent = ReadLastLifecycleEvent(path);

        if (lastEvent == "task_complete")
            return (SessionStatus.AwaitingInput, "finished", lastWrite);

        if (lastEvent is not null && lastEvent.Contains("approval", StringComparison.OrdinalIgnoreCase))
            return (SessionStatus.AwaitingInput, "needs approval", lastWrite);

        if (recentlyActive || lastEvent == "task_started")
            return (SessionStatus.Working, null, lastWrite);

        return (SessionStatus.Unknown, null, lastWrite);
    }

    /// <summary>Returns the payload type of the last turn-lifecycle <c>event_msg</c>.</summary>
    private static string? ReadLastLifecycleEvent(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long start = Math.Max(0, fs.Length - TailBytes);
            fs.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8);

            string? last = null;
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var type = LifecycleType(line);
                if (type is not null)
                    last = type;
            }
            return last;
        }
        catch
        {
            return null;
        }
    }

    private static string? LifecycleType(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var t) || t.GetString() != "event_msg")
                return null;
            if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
                return null;
            if (!payload.TryGetProperty("type", out var pt))
                return null;

            var ptype = pt.GetString();
            if (ptype is "task_started" or "task_complete"
                || (ptype is not null && ptype.Contains("approval", StringComparison.OrdinalIgnoreCase)))
                return ptype;

            return null;
        }
        catch
        {
            return null;
        }
    }
}
