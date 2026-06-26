using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentMonitor.Providers.ClaudeCode.Internal;

/// <summary>A status marker written by the hook sink for one session.</summary>
internal sealed class HookStatusRecord
{
    [JsonPropertyName("sessionId")] public string? SessionId { get; set; }

    /// <summary>The hook event name: Stop, Notification, UserPromptSubmit, ...</summary>
    [JsonPropertyName("event")] public string? Event { get; set; }

    /// <summary>Optional message, e.g. a Notification's text.</summary>
    [JsonPropertyName("message")] public string? Message { get; set; }

    [JsonPropertyName("timestamp")] public DateTimeOffset? Timestamp { get; set; }
}

/// <summary>Reads hook status markers written by the hook sink. Returns null when
/// hooks aren't installed or no event has fired for a session yet.</summary>
internal sealed class HookStatusStore
{
    private readonly ClaudePaths _paths;

    public HookStatusStore(ClaudePaths paths) => _paths = paths;

    /// <summary>True if the hook status directory exists (hooks have run at least once).</summary>
    public bool IsActive => Directory.Exists(_paths.HookStatusDir);

    public HookStatusRecord? TryRead(string sessionId)
    {
        var file = _paths.HookStatusFile(sessionId);
        if (!File.Exists(file))
            return null;

        try
        {
            using var stream = new FileStream(
                file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return JsonSerializer.Deserialize<HookStatusRecord>(stream);
        }
        catch
        {
            return null;
        }
    }
}
