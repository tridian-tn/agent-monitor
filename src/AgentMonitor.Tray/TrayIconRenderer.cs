using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using AgentMonitor.Core.Status;

namespace AgentMonitor.Tray;

/// <summary>Renders the traffic-light tray icons once at startup and caches them.</summary>
internal sealed class TrayIconRenderer : IDisposable
{
    private readonly Dictionary<TrayColor, Icon> _icons = new();
    private readonly List<IntPtr> _handles = new();

    public TrayIconRenderer()
    {
        _icons[TrayColor.Red] = Build(Color.FromArgb(220, 53, 47));
        _icons[TrayColor.Amber] = Build(Color.FromArgb(240, 160, 30));
        _icons[TrayColor.Green] = Build(Color.FromArgb(40, 180, 80));
    }

    public Icon Get(TrayColor color) => _icons[color];

    private Icon Build(Color color)
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var fill = new SolidBrush(color);
            g.FillEllipse(fill, 4, 4, 24, 24);

            using var ring = new Pen(Color.FromArgb(70, 0, 0, 0), 2f);
            g.DrawEllipse(ring, 4, 4, 24, 24);

            using var gloss = new SolidBrush(Color.FromArgb(70, 255, 255, 255));
            g.FillEllipse(gloss, 10, 8, 9, 6);
        }

        IntPtr handle = bitmap.GetHicon();
        _handles.Add(handle);
        return (Icon)Icon.FromHandle(handle).Clone();
    }

    public void Dispose()
    {
        foreach (var icon in _icons.Values)
            icon.Dispose();
        foreach (var handle in _handles)
            DestroyIcon(handle);
        _handles.Clear();
        _icons.Clear();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);
}
