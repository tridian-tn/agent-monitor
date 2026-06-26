using System.Runtime.InteropServices;

namespace AgentMonitor.Tray;

/// <summary>
/// Detects the foreground window's process and whether a given process shares its
/// lineage. A session is treated as "on screen" when the focused window and the
/// session process are on the same parent/child chain:
/// <list type="bullet">
///   <item>CLI Claude Code — the <c>claude</c> process is a descendant of the
///   focused terminal window, so focus is per-terminal-window precise.</item>
///   <item>Claude Desktop — every session process is a child of the single
///   <c>Claude</c> window process, so focusing the app marks them all seen
///   (the OS exposes no per-tab signal).</item>
/// </list>
/// </summary>
internal static class ForegroundWindow
{
    public static int? GetForegroundPid()
    {
        var handle = GetForegroundWindow();
        if (handle == IntPtr.Zero)
            return null;
        _ = GetWindowThreadProcessId(handle, out int pid);
        return pid == 0 ? null : pid;
    }

    /// <summary>Maps each process id to its parent via a single Toolhelp snapshot.</summary>
    public static Dictionary<int, int> BuildParentMap()
    {
        var map = new Dictionary<int, int>();
        IntPtr snapshot = CreateToolhelp32Snapshot(TH32CS_SNAPPROCESS, 0);
        if (snapshot == InvalidHandle)
            return map;

        try
        {
            var entry = new PROCESSENTRY32 { dwSize = (uint)Marshal.SizeOf<PROCESSENTRY32>() };
            if (Process32First(snapshot, ref entry))
            {
                do
                {
                    map[(int)entry.th32ProcessID] = (int)entry.th32ParentProcessID;
                }
                while (Process32Next(snapshot, ref entry));
            }
        }
        finally
        {
            CloseHandle(snapshot);
        }

        return map;
    }

    /// <summary>True if <paramref name="a"/> and <paramref name="b"/> are on the
    /// same ancestor chain (one is an ancestor of, or equal to, the other).</summary>
    public static bool ShareLineage(int a, int b, Dictionary<int, int> parents)
        => ReachesAncestor(a, b, parents) || ReachesAncestor(b, a, parents);

    private static bool ReachesAncestor(int from, int target, Dictionary<int, int> parents)
    {
        var visited = new HashSet<int>();
        int current = from;
        while (current != 0 && visited.Add(current))
        {
            if (current == target)
                return true;
            if (!parents.TryGetValue(current, out current))
                break;
        }
        return false;
    }

    private const uint TH32CS_SNAPPROCESS = 0x00000002;
    private static readonly IntPtr InvalidHandle = new(-1);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport("kernel32.dll")]
    private static extern bool Process32First(IntPtr snapshot, ref PROCESSENTRY32 entry);

    [DllImport("kernel32.dll")]
    private static extern bool Process32Next(IntPtr snapshot, ref PROCESSENTRY32 entry);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(IntPtr handle);
}
