using AgentMonitor.Core.Sessions;

namespace AgentMonitor.Providers.Codex;

/// <summary>
/// Placeholder provider for the OpenAI Codex CLI. It exists to demonstrate the
/// extension point: to add Codex (or any other tool) support, implement
/// <see cref="ISessionProvider"/>, discover that tool's own session/log state,
/// and map it onto the shared <see cref="SessionStatus"/> model — the tray and
/// the status policies need no changes.
/// </summary>
public sealed class CodexProvider : ISessionProvider
{
    public string Id => "codex";
    public string DisplayName => "Codex";

    // TODO: detect the Codex CLI (e.g. a ~/.codex directory, or the codex binary
    // on PATH) and set this accordingly.
    public bool IsInstalled => false;

    // TODO: read Codex session state and translate to AgentSession entries.
    // Codex keeps rollout/session logs under ~/.codex/sessions; a real
    // implementation would tail the active rollout to derive Working /
    // AwaitingInput, mirroring TranscriptStatusReader in the Claude provider.
    public IReadOnlyList<AgentSession> GetSessions() => Array.Empty<AgentSession>();
}
