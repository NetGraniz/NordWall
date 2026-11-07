using System.Text.Json;

namespace Nordwall;

internal sealed class SettingsStore
{
    private readonly string _path;
    public string? LastWallpaper { get; private set; }

    public SettingsStore()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nordwall");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "settings.json");

        try
        {
            if (File.Exists(_path))
                LastWallpaper = JsonSerializer.Deserialize<SavedSettings>(File.ReadAllText(_path))?.LastWallpaper;
        }
        catch (JsonException)
        {
            // A corrupted settings file should never prevent startup.
        }
        catch (IOException)
        {
            // The app can still run with the first available wallpaper.
        }
        catch (UnauthorizedAccessException)
        {
            // Settings are optional; the app will still load its wallpapers.
        }
    }

    public void SaveSelection(string name)
    {
        LastWallpaper = name;

        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(new SavedSettings { LastWallpaper = name }));
        }
        catch (IOException)
        {
            // Losing the remembered selection is non-fatal.
        }
        catch (UnauthorizedAccessException)
        {
            // The desktop wallpaper remains functional without settings persistence.
        }
    }

    private sealed class SavedSettings
    {
        public SavedSettings() { }
        public string? LastWallpaper { get; set; }
    }
}
