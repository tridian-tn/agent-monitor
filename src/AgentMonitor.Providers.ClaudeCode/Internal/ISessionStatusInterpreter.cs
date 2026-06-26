using AgentMonitor.Core.Sessions;

namespace AgentMonitor.Providers.ClaudeCode.Internal;

/// <summary>
/// Strategy for deciding a session's status from its registry record. The default
/// implementation reads transcripts; future implementations could read Stop /
/// Notification hook output or the daemon tempo/needs store for exact status.
/// </summary>
internal interface ISessionStatusInterpreter
{
    (SessionStatus Status, string? Detail, DateTimeOffset? LastActivity) Interpret(SessionRecord record);
}
