using System.Text.Json;

namespace AgentMonitor.Providers.ClaudeCode.Internal;

/// <summary>Reads the live session registry from <c>~/.claude/sessions</c>.</summary>
internal sealed class SessionRegistryReader
{
    private readonly ClaudePaths _paths;

    public SessionRegistryReader(ClaudePaths paths) => _paths = paths;

    public IReadOnlyList<SessionRecord> Read()
    {
        var result = new List<SessionRecord>();
        if (!Directory.Exists(_paths.SessionsDir))
            return result;

        foreach (var file in SafeEnumerate(_paths.SessionsDir))
        {
            try
            {
                using var stream = new FileStream(
                    file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var record = JsonSerializer.Deserialize<SessionRecord>(stream);
                if (record is { Pid: > 0, SessionId.Length: > 0 })
                    result.Add(record);
            }
            catch
            {
                // Skip files that are partially written or locked this tick.
            }
        }

        return result;
    }

    private static IEnumerable<string> SafeEnumerate(string dir)
    {
        try
        {
            return Directory.EnumerateFiles(dir, "*.json");
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}
