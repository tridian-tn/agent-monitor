using AgentMonitor.Core.Sessions;
using AgentMonitor.Core.Status;
using AgentMonitor.Providers.ClaudeCode;
using AgentMonitor.Providers.Codex;

namespace AgentMonitor.Tray;

/// <summary>
/// Owns the tray icon and the polling loop. Providers are injected as a list, so
/// adding a new LLM tool is just adding another <see cref="ISessionProvider"/>.
/// The colour comes from <see cref="AttentionTracker"/>: green only when a session
/// recently became ready and you haven't looked yet.
/// </summary>
internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly TrayIconRenderer _renderer = new();
    private readonly StatusAggregator _aggregator;
    private readonly AttentionTracker _tracker = new();
    private readonly System.Windows.Forms.Timer _timer;

    private readonly HookInstaller _hooks = new();
    private readonly HashSet<string> _notifiedNeedsYou = new();
    private bool _notificationsEnabled = true;

    private AggregateStatus? _lastStatus;
    private TrayColor _lastColor = TrayColor.Grey;
    private HashSet<string> _lastNeedsYou = new();

    public TrayApplicationContext()
    {
        var providers = new ISessionProvider[]
        {
            new ClaudeCodeProvider(),
            new CodexProvider(), // stub — demonstrates multi-provider support
        };
        _aggregator = new StatusAggregator(providers);

        _notifyIcon = new NotifyIcon
        {
            Icon = _renderer.Get(TrayColor.Grey),
            Visible = true,
            Text = "Agent Monitor",
            ContextMenuStrip = new ContextMenuStrip(),
        };
        _notifyIcon.ContextMenuStrip!.Opening += (_, _) => RebuildMenu();

        _timer = new System.Windows.Forms.Timer { Interval = 1000 };
        _timer.Tick += (_, _) => Refresh();
        _timer.Start();

        Refresh();
    }

    private void Refresh()
    {
        var status = _aggregator.Compute();
        var (color, needsYou) = _tracker.Evaluate(
            status.Sessions,
            DateTimeOffset.UtcNow,
            ForegroundWindow.GetForegroundPid,
            ForegroundWindow.BuildParentMap);

        _lastStatus = status;
        _lastColor = color;
        _lastNeedsYou = needsYou.Select(SessionKey).ToHashSet();

        _notifyIcon.Icon = _renderer.Get(color);
        _notifyIcon.Text = Truncate($"{color}: {status.Summary}", 63);

        NotifyNeedsYou(needsYou);
    }

    /// <summary>Balloons the moment a session newly enters the "needs you now" set.</summary>
    private void NotifyNeedsYou(IReadOnlyList<AgentSession> needsYou)
    {
        var current = needsYou.Select(SessionKey).ToHashSet();
        _notifiedNeedsYou.RemoveWhere(k => !current.Contains(k));

        foreach (var session in needsYou)
        {
            if (!_notifiedNeedsYou.Add(SessionKey(session)))
                continue; // already pinged for this episode

            if (_notificationsEnabled)
            {
                var detail = string.IsNullOrEmpty(session.Detail) ? "is ready for you" : session.Detail;
                _notifyIcon.ShowBalloonTip(3000, $"{session.Title} — ready for you", detail, ToolTipIcon.Info);
            }
        }
    }

    private static string SessionKey(AgentSession s) => $"{s.ProviderId}/{s.SessionId}";

    private void RebuildMenu()
    {
        var menu = _notifyIcon.ContextMenuStrip!;
        menu.Items.Clear();

        var status = _lastStatus ?? _aggregator.Compute();

        menu.Items.Add(new ToolStripMenuItem($"{_lastColor} — {status.Summary}") { Enabled = false });
        menu.Items.Add(new ToolStripSeparator());

        if (status.Sessions.Count == 0)
        {
            menu.Items.Add(new ToolStripMenuItem("No active sessions") { Enabled = false });
        }
        else
        {
            foreach (var session in status.Sessions.OrderBy(s => s.Title, StringComparer.OrdinalIgnoreCase))
                menu.Items.Add(new ToolStripMenuItem(DescribeSession(session)) { Enabled = false });
        }

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(BuildPreciseModeMenu());

        var refresh = new ToolStripMenuItem("Refresh now");
        refresh.Click += (_, _) => Refresh();
        menu.Items.Add(refresh);

        menu.Items.Add(new ToolStripSeparator());

        var startup = new ToolStripMenuItem("Start with Windows")
        {
            Checked = SafeStartupEnabled(),
            CheckOnClick = true,
        };
        startup.Click += (_, _) => SetStartup(startup.Checked);
        menu.Items.Add(startup);

        var notify = new ToolStripMenuItem("Notify when ready")
        {
            Checked = _notificationsEnabled,
            CheckOnClick = true,
        };
        notify.Click += (_, _) => _notificationsEnabled = notify.Checked;
        menu.Items.Add(notify);

        var about = new ToolStripMenuItem("About…");
        about.Click += (_, _) =>
        {
            using var form = new AboutForm();
            form.ShowDialog();
        };
        menu.Items.Add(about);

        menu.Items.Add(new ToolStripSeparator());

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitThread();
        menu.Items.Add(exit);
    }

    private static bool SafeStartupEnabled()
    {
        try { return StartupManager.IsEnabled(); }
        catch { return false; }
    }

    private void SetStartup(bool enabled)
    {
        try
        {
            StartupManager.SetEnabled(enabled);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not change the startup setting:\n{ex.Message}",
                "Agent Monitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private ToolStripMenuItem BuildPreciseModeMenu()
    {
        var menu = new ToolStripMenuItem("Precise mode (hooks)");

        if (!_hooks.CanInstall)
        {
            menu.DropDownItems.Add(new ToolStripMenuItem("Hook sink not found") { Enabled = false });
            return menu;
        }

        bool installed = SafeIsInstalled();
        menu.DropDownItems.Add(new ToolStripMenuItem(installed ? "Status: installed" : "Status: not installed")
        {
            Enabled = false,
        });
        menu.DropDownItems.Add(new ToolStripSeparator());

        var install = new ToolStripMenuItem("Install hooks") { Enabled = !installed };
        install.Click += (_, _) => ToggleHooks(install: true);
        menu.DropDownItems.Add(install);

        var remove = new ToolStripMenuItem("Remove hooks") { Enabled = installed };
        remove.Click += (_, _) => ToggleHooks(install: false);
        menu.DropDownItems.Add(remove);

        return menu;
    }

    private bool SafeIsInstalled()
    {
        try { return _hooks.IsInstalled(); }
        catch { return false; }
    }

    private void ToggleHooks(bool install)
    {
        try
        {
            if (install)
                _hooks.Install();
            else
                _hooks.Uninstall();

            MessageBox.Show(
                $"Hooks {(install ? "installed" : "removed")}.\n\nSettings: {_hooks.SettingsPath}\n" +
                "A backup was saved as settings.json.bak.\n\n" +
                "New Claude Code sessions will report precise status; existing sessions " +
                "pick it up on their next turn.",
                "Agent Monitor", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not update hooks:\n{ex.Message}",
                "Agent Monitor", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        Refresh();
    }

    private string DescribeSession(AgentSession session)
    {
        bool needsYou = _lastNeedsYou.Contains(SessionKey(session));

        var glyph = needsYou
            ? "★"                                  // ready and unseen
            : session.Status switch
            {
                SessionStatus.AwaitingInput => "✓", // done, already seen / stale
                SessionStatus.Working => "…",        // working
                SessionStatus.Error => "!",
                _ => "·",
            };

        var state = needsYou
            ? "ready for you"
            : session.Status switch
            {
                SessionStatus.AwaitingInput => "waiting (seen)",
                SessionStatus.Working => "working",
                SessionStatus.Idle => "idle",
                SessionStatus.Error => "error",
                _ => "unknown",
            };

        var detail = string.IsNullOrEmpty(session.Detail) ? "" : $" ({session.Detail})";
        var origin = string.IsNullOrEmpty(session.Origin) ? "" : $" [{session.Origin}]";
        return $"{glyph} {session.Title} — {state}{detail}{origin}";
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max];

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _timer.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _renderer.Dispose();
        }
        base.Dispose(disposing);
    }
}
