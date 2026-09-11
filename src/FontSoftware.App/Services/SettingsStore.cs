using System.IO;
using System.Text.Json;

namespace FontSoftware.App.Services;

/// <summary>
/// Loads and saves <see cref="AppSettings"/> under %AppData% (FR-6: settings
/// persist locally and survive restarts). A missing/corrupt file yields sane
/// defaults so a fresh install always starts cleanly.
/// </summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string _path;

    public SettingsStore()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TextSoft");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
                return new AppSettings();
            string json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json, Options) ?? new AppSettings();
        }
        catch
        {
            // Corrupt file → fall back to defaults rather than crashing on launch.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, Options);
        File.WriteAllText(_path, json);
    }
}
