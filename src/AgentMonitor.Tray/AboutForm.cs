using System.Reflection;

namespace AgentMonitor.Tray;

/// <summary>A small modal About dialog showing the product name and build version.</summary>
internal sealed class AboutForm : Form
{
    public AboutForm()
    {
        Text = "About Agent Monitor";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(380, 196);

        var title = new Label
        {
            Text = "Agent Monitor",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(18, 18),
        };

        var version = new Label
        {
            Text = $"Version {GetVersion()}",
            AutoSize = true,
            Location = new Point(20, 54),
        };

        var description = new Label
        {
            Text = "A system-tray traffic light for local LLM coding agents "
                 + "(Claude Code, and more behind one provider interface).",
            Location = new Point(20, 84),
            Size = new Size(344, 56),
        };

        var ok = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Size = new Size(84, 30),
            Location = new Point(280, 150),
        };

        Controls.AddRange(new Control[] { title, version, description, ok });
        AcceptButton = ok;
        CancelButton = ok;
    }

    private static string GetVersion()
    {
        var assembly = Assembly.GetEntryAssembly();
        return assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly?.GetName().Version?.ToString()
            ?? "unknown";
    }
}
