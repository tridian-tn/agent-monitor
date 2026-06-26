using System.Text.Json.Serialization;

namespace AgentMonitor.Providers.ClaudeCode.Internal;

/// <summary>
/// Mirrors a <c>~/.claude/sessions/&lt;pid&gt;.json</c> registry record. All
/// Claude-internal field names are confined to this DTO so format drift is
/// contained to one place.
/// </summary>
internal sealed class SessionRecord
{
    [JsonPropertyName("pid")] public int Pid { get; set; }
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = "";
    [JsonPropertyName("cwd")] public string? Cwd { get; set; }
    [JsonPropertyName("startedAt")] public long StartedAt { get; set; }
    [JsonPropertyName("procStart")] public string? ProcStart { get; set; }
    [JsonPropertyName("version")] public string? Version { get; set; }

    /// <summary>"interactive", "bg"/background, "print", ...</summary>
    [JsonPropertyName("kind")] public string? Kind { get; set; }

    /// <summary>Label only: "cli" by default, "claude-desktop" from the desktop app.</summary>
    [JsonPropertyName("entrypoint")] public string? Entrypoint { get; set; }

    // The following are populated for daemon/background sessions and provide an
    // explicit, high-fidelity status that the interpreter prefers when present.

    /// <summary>"active" | "idle" | "blocked".</summary>
    [JsonPropertyName("tempo")] public string? Tempo { get; set; }

    /// <summary>What a blocked session needs, e.g. "awaiting input".</summary>
    [JsonPropertyName("needs")] public string? Needs { get; set; }

    [JsonPropertyName("detail")] public string? Detail { get; set; }
}
