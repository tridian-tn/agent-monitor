using System.Diagnostics;

namespace AgentMonitor.Providers.Codex.Internal;

/// <summary>Detects whether any Codex process is running. Codex has no per-session
/// PID registry, so this is a coarse "is Codex up at all" gate used alongside
/// rollout recency.</summary>
internal static class CodexProcess
{
    public static bool IsRunning()
    {
        try
        {
            // GetProcessesByName matches the base name case-insensitively, so this
            // covers both the "codex" CLI and the "Codex" desktop app.
            var processes = Process.GetProcessesByName("codex");
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var p in processes)
                    p.Dispose();
            }
        }
        catch
        {
            return false;
        }
    }
}
