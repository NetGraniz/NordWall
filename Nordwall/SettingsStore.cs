using System.Text.Json;
using System.Text.Json.Serialization;

namespace Nordwall;

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum WallpaperMode
{
    Separate,
    Span
}

internal sealed class DisplayPreferences
{
    // Retained for automatic migration from v0.3's settings.json.
    public string? LastWallpaper { get; set; }
    public WallpaperMode Mode { get; set; } = WallpaperMode.Separate;
    public string? SpanWallpaper { get; set; }
    public Dictionary<string, string?> PerMonitorWallpapers { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
    public bool AutoPauseFullscreen { get; set; } = true;

    public DisplayPreferences Clone() => new()
    {
        LastWallpaper = LastWallpaper,
        Mode = Mode,
        SpanWallpaper = SpanWallpaper,
        PerMonitorWallpapers = new Dictionary<string, string?>(
            PerMonitorWallpapers, StringComparer.OrdinalIgnoreCase),
        AutoPauseFullscreen = AutoPauseFullscreen
    };
}

internal sealed class SettingsStore
{
    private readonly string _path;
    public DisplayPreferences Current { get; private set; } = new();

    public SettingsStore()
    {
        string directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nordwall");
        Directory.CreateDirectory(directory);
        _path = Path.Combine(directory, "settings.json");

        try
        {
            if (File.Exists(_path))
                Current = JsonSerializer.Deserialize<DisplayPreferences>(
                    File.ReadAllText(_path)) ?? new DisplayPreferences();

            Current.PerMonitorWallpapers ??= new(StringComparer.OrdinalIgnoreCase);
            if (!Enum.IsDefined(Current.Mode))
                Current.Mode = WallpaperMode.Separate;
        }
        catch (Exception ex) when (ex is JsonException or IOException
                                   or UnauthorizedAccessException or NotSupportedException)
        {
            // Invalid or inaccessible settings must never prevent startup.
            Current = new DisplayPreferences();
        }
    }

    public void Save(DisplayPreferences preferences)
    {
        Current = preferences.Clone();
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(Current,
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The running wallpaper still uses the selected settings.
        }
    }
}
