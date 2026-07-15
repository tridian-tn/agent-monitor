using AgentMonitor.Tray;
using Xunit;

public class AppSettingsTests : IDisposable
{
    private readonly string _root;
    private readonly string _path;

    public AppSettingsTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "amset-" + Guid.NewGuid().ToString("N")[..8]);
        _path = Path.Combine(_root, "settings.json");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    [Fact]
    public void Load_missing_file_returns_defaults()
    {
        var settings = AppSettings.Load(_path);

        Assert.True(settings.FlashOnReady); // on out of the box
        Assert.Equal(_path, settings.FilePath);
    }

    [Fact]
    public void Save_then_Load_round_trips_the_value()
    {
        var settings = AppSettings.Load(_path);
        settings.FlashOnReady = false;
        settings.Save();

        Assert.True(File.Exists(_path));
        Assert.False(AppSettings.Load(_path).FlashOnReady);
    }

    [Fact]
    public void Load_corrupt_file_falls_back_to_defaults()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(_path, "{ not valid json");

        var settings = AppSettings.Load(_path);

        Assert.True(settings.FlashOnReady); // corrupt file → same defaults
        Assert.Equal(_path, settings.FilePath);
    }
}
