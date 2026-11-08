using AgentMonitor.Core.Sessions;

namespace AgentMonitor.Providers.ClaudeCode.Internal;

/// <summary>
/// Strategy for deciding a session's status from its registry record.
/// <see cref="HeuristicStatusInterpreter"/> uses the daemon tempo/needs fields or the
/// transcript; <see cref="HookStatusInterpreter"/> layers hook events on top of it.
/// </summary>
internal interface ISessionStatusInterpreter
{
    (SessionStatus Status, string? Detail, DateTimeOffset? LastActivity) Interpret(SessionRecord record);
}
