using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentMonitor.Tray;

/// <summary>
/// Small bag of user-tweakable settings persisted as JSON under
/// <c>%AppData%\AgentMonitor\settings.json</c>. Deliberately best-effort: a missing
/// or corrupt file loads defaults, and a failed save just means a toggle won't stick.
/// </summary>
internal sealed class AppSettings
{
    private static string DefaultPath => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AgentMonitor", "settings.json");

    /// <summary>Flash a green circle on the active monitor when a session becomes ready.</summary>
    public bool FlashOnReady { get; set; } = true;

    /// <summary>Where this instance loads from and saves to. Not itself persisted.</summary>
    [JsonIgnore]
    public string FilePath { get; set; } = DefaultPath;

    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (File.Exists(path))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions);
                if (loaded is not null)
                {
                    loaded.FilePath = path;
                    return loaded;
                }
            }
        }
        catch
        {
            // Unreadable or malformed settings fall back to defaults.
        }

        return new AppSettings { FilePath = path };
    }

    public void Save()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // Best-effort; persistence is a convenience, not a requirement.
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
