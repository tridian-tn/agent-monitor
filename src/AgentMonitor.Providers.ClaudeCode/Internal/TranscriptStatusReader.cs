using System.Text;
using System.Text.Json;
using AgentMonitor.Core.Sessions;

namespace AgentMonitor.Providers.ClaudeCode.Internal;

/// <summary>
/// Tails a session <c>.jsonl</c> transcript and infers status from the recency of
/// writes plus the most recent assistant record's <c>stop_reason</c>.
///
/// Empirically, an idle interactive session usually rests at a <c>tool_use</c>
/// record (paused at a permission prompt or between turns), not a clean
/// <c>end_turn</c>. So the rules are:
/// <list type="bullet">
///   <item><c>end_turn</c> at any time =&gt; AwaitingInput ("finished").</item>
///   <item>written within <paramref name="workingWindow"/> =&gt; Working (streaming).</item>
///   <item>quiet for at least <paramref name="idleThreshold"/> =&gt; AwaitingInput
///   ("idle" — finished, blocked on approval, or a long-running tool).</item>
///   <item>in between =&gt; Working (a tool is probably still running).</item>
/// </list>
/// The idle case is inherently ambiguous from the transcript alone; Stop /
/// Notification hooks resolve it exactly.
/// </summary>
internal sealed class TranscriptStatusReader
{
    private const int TailBytes = 64 * 1024;

    public (SessionStatus Status, string? Detail, DateTimeOffset? LastActivity) Read(
        string path, TimeSpan workingWindow, TimeSpan idleThreshold)
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

        TimeSpan age = DateTimeOffset.UtcNow - lastWrite;
        string? lastAssistantStop = ReadLastAssistantStopReason(path);

        // A finished turn is the unambiguous "awaiting you" signal.
        if (lastAssistantStop == "end_turn")
            return (SessionStatus.AwaitingInput, "finished", lastWrite);

        // Actively appending => the agent is streaming output / working.
        if (age <= workingWindow)
            return (SessionStatus.Working, null, lastWrite);

        // Quiet for a while at a non-finished record: most often paused on a
        // permission prompt or otherwise waiting on you. Surface it as awaiting
        // input (a long-running tool will occasionally show here — hooks fix that).
        if (age >= idleThreshold)
            return (SessionStatus.AwaitingInput, "idle", lastWrite);

        // Recently paused: a tool is probably still running.
        return (SessionStatus.Working, null, lastWrite);
    }

    private static string? ReadLastAssistantStopReason(string path)
    {
        try
        {
            using var fs = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long start = Math.Max(0, fs.Length - TailBytes);
            fs.Seek(start, SeekOrigin.Begin);
            using var reader = new StreamReader(fs, Encoding.UTF8);

            string? lastStop = null;
            string? line;
            while ((line = reader.ReadLine()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var parsed = ParseAssistantStop(line);
                if (parsed.IsAssistant)
                    lastStop = parsed.StopReason; // remember most recent assistant turn
            }

            return lastStop;
        }
        catch
        {
            return null;
        }
    }

    private static (bool IsAssistant, string? StopReason) ParseAssistantStop(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            if (!root.TryGetProperty("type", out var type) || type.GetString() != "assistant")
                return (false, null);

            if (root.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.Object
                && message.TryGetProperty("stop_reason", out var stop))
            {
                return (true, stop.GetString());
            }

            return (true, null);
        }
        catch
        {
            // A partial first line (we seeked into the middle of the file) or any
            // non-JSON line: ignore it.
            return (false, null);
        }
    }
}
