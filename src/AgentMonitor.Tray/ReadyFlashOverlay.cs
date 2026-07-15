using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace AgentMonitor.Tray;

/// <summary>
/// A brief, click-through green circle that appears in the top-right corner of the
/// active monitor when a session becomes ready — the same green as the tray icon,
/// scaled up to catch your eye. It never takes focus, passes clicks through to
/// whatever is beneath it, and fades itself away after about five seconds.
/// </summary>
internal sealed class ReadyFlashOverlay : IDisposable
{
    private const int Diameter = 120;
    private const int Margin = 32;

    private FlashForm? _current;

    /// <summary>Shows (or restarts) the flash on whichever monitor is active now.</summary>
    public void Flash()
    {
        var bounds = TopRight(ActiveScreen().WorkingArea);

        if (_current is { IsDisposed: false })
        {
            _current.Restart(bounds);
            return;
        }

        _current = new FlashForm();
        _current.FormClosed += (_, _) => _current = null;
        _current.Restart(bounds);
    }

    private static Rectangle TopRight(Rectangle workingArea) => new(
        workingArea.Right - Diameter - Margin,
        workingArea.Top + Margin,
        Diameter, Diameter);

    private static Screen ActiveScreen()
    {
        var foreground = GetForegroundWindow();
        if (foreground != IntPtr.Zero)
            return Screen.FromHandle(foreground);
        return Screen.PrimaryScreen ?? Screen.AllScreens[0];
    }

    public void Dispose()
    {
        _current?.Dispose();
        _current = null;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    /// <summary>The borderless, transparent, non-activating circle window itself.</summary>
    private sealed class FlashForm : Form
    {
        private const int WS_EX_LAYERED = 0x00080000;
        private const int WS_EX_TRANSPARENT = 0x00000020;   // clicks fall through
        private const int WS_EX_TOOLWINDOW = 0x00000080;    // stays out of Alt+Tab
        private const int WS_EX_NOACTIVATE = 0x08000000;    // never steals focus

        private static readonly Color TrayGreen = Color.FromArgb(40, 180, 80); // matches the tray icon

        private const double PeakOpacity = 0.9;
        private const int StepMs = 40;
        private const int HoldMs = 4300;   // fully visible, then...
        private const int FadeMs = 700;    // ...fades to nothing

        private readonly System.Windows.Forms.Timer _timer;
        private int _elapsed;

        public FlashForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            DoubleBuffered = true;
            BackColor = TrayGreen;
            Opacity = PeakOpacity;

            _timer = new System.Windows.Forms.Timer { Interval = StepMs };
            _timer.Tick += (_, _) => OnStep();
        }

        /// <summary>Positions the circle and (re)starts its show-then-fade cycle.</summary>
        public void Restart(Rectangle bounds)
        {
            Bounds = bounds;
            _elapsed = 0;
            Opacity = PeakOpacity;

            if (!Visible)
                Show();

            _timer.Stop();
            _timer.Start();
        }

        private void OnStep()
        {
            _elapsed += StepMs;

            if (_elapsed >= HoldMs + FadeMs)
            {
                _timer.Stop();
                Close();
                return;
            }

            if (_elapsed > HoldMs)
            {
                double fadeProgress = (_elapsed - HoldMs) / (double)FadeMs;
                Opacity = PeakOpacity * (1 - fadeProgress);
            }
        }

        // Show without stealing focus from whatever the user is looking at.
        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE;
                return cp;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            // Clip the window to a circle so its corners don't paint as a green square.
            using var path = new GraphicsPath();
            path.AddEllipse(0, 0, ClientSize.Width, ClientSize.Height);
            Region = new Region(path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var rect = new Rectangle(3, 3, ClientSize.Width - 6, ClientSize.Height - 6);

            using var fill = new SolidBrush(TrayGreen);
            g.FillEllipse(fill, rect);

            using var ring = new Pen(Color.FromArgb(90, 0, 0, 0), 3f);
            g.DrawEllipse(ring, rect);

            using var gloss = new SolidBrush(Color.FromArgb(80, 255, 255, 255));
            g.FillEllipse(gloss, rect.Width / 4, rect.Height / 6, rect.Width / 3, rect.Height / 5);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _timer.Dispose();
            base.Dispose(disposing);
        }
    }
}
