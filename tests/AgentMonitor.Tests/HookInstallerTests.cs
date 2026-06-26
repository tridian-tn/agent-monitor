using System.Text.Json.Nodes;
using AgentMonitor.Tray;
using Xunit;

public class HookInstallerTests : IDisposable
{
    private readonly string _root;
    private readonly string _settings;

    public HookInstallerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "amii-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
        _settings = Path.Combine(_root, "settings.json");
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", _root);

        // A stub sink next to the test assembly so the installer resolves a command.
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "AgentMonitor.HookSink.exe"), "stub");

        // Seed an existing, unrelated setting and Stop hook to prove preservation.
        File.WriteAllText(_settings,
            "{\"model\":\"opus\",\"hooks\":{\"Stop\":[{\"hooks\":[{\"type\":\"command\",\"command\":\"echo existing\"}]}]}}");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("CLAUDE_CONFIG_DIR", null);
        try { Directory.Delete(_root, true); } catch { }
    }

    private JsonObject Read() => (JsonObject)JsonNode.Parse(File.ReadAllText(_settings))!;

    private static bool Has(JsonObject root, string evt, string contains)
        => (root["hooks"]?[evt] as JsonArray)?
            .SelectMany(g => (g?["hooks"] as JsonArray) ?? new JsonArray())
            .Any(h => (h?["command"]?.GetValue<string>() ?? "").Contains(contains)) ?? false;

    [Fact]
    public void Install_merges_without_clobbering()
    {
        var installer = new HookInstaller();
        Assert.True(installer.CanInstall);
        Assert.False(installer.IsInstalled());

        installer.Install();
        var settings = Read();

        Assert.True(installer.IsInstalled());
        Assert.True(Has(settings, "UserPromptSubmit", "AgentMonitor.HookSink"));
        Assert.True(Has(settings, "Notification", "AgentMonitor.HookSink"));
        Assert.True(Has(settings, "SessionEnd", "AgentMonitor.HookSink"));
        Assert.True(Has(settings, "Stop", "echo existing"));       // preserved
        Assert.Equal("opus", settings["model"]?.GetValue<string>()); // preserved
        Assert.True(File.Exists(_settings + ".bak"));
    }

    [Fact]
    public void Install_is_idempotent()
    {
        var installer = new HookInstaller();
        installer.Install();
        int before = ((JsonArray)Read()["hooks"]!["Stop"]!).Count;

        installer.Install();
        Assert.Equal(before, ((JsonArray)Read()["hooks"]!["Stop"]!).Count);
    }

    [Fact]
    public void Uninstall_removes_only_ours()
    {
        var installer = new HookInstaller();
        installer.Install();
        installer.Uninstall();
        var settings = Read();

        Assert.False(installer.IsInstalled());
        Assert.False(Has(settings, "UserPromptSubmit", "AgentMonitor.HookSink"));
        Assert.True(Has(settings, "Stop", "echo existing"));
        Assert.Equal("opus", settings["model"]?.GetValue<string>());
    }
}
