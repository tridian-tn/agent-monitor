using System.Diagnostics;

namespace AgentMonitor.Providers.ClaudeCode.Internal;

/// <summary>
/// Confirms a registry record refers to a still-running Claude process, guarding
/// against stale files left by abruptly-killed sessions (common for terminals)
/// and against PID reuse.
/// </summary>
internal static class ProcessLiveness
{
    public static bool IsAlive(SessionRecord record)
    {
        try
        {
            using var process = Process.GetProcessById(record.Pid);
            if (process.HasExited)
                return false;

            // Guard against PID reuse: the process should still look like Claude.
            // (A stricter check could compare process start time against
            // record.ProcStart, which holds DateTime ticks.)
            return process.ProcessName.Contains("claude", StringComparison.OrdinalIgnoreCase);
        }
        catch (ArgumentException)
        {
            return false; // no process with that id
        }
        catch (InvalidOperationException)
        {
            return false; // process exited between calls
        }
        catch
        {
            return true; // access denied etc. — surface rather than hide
        }
    }
}
