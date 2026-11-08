using Microsoft.Web.WebView2.Core;

namespace Nordwall;

/// <summary>
/// Manages one WebView2 per display in Separate mode, or one shared WebView2
/// covering the whole virtual desktop in Span mode. A single span renderer
/// keeps the animation synchronized and avoids duplicate video decoding.
/// </summary>
internal sealed class WallpaperController : IDisposable
{
    private const string SpanKey = "__span__";

    private readonly string _wallpapersDirectory;
    private readonly string _assetsDirectory;
    private readonly SettingsStore _settings;
    private readonly Action<string> _reportError;
    private readonly System.Windows.Forms.Timer _pollTimer;
    private readonly Dictionary<string, WallpaperForm> _views =
        new(StringComparer.OrdinalIgnoreCase);

    private CoreWebView2Environment? _environment;
    private string _displaySignature = string.Empty;
    private bool _configuring;
    private bool _reconfigurePending;
    private bool _checking;
    private bool _running;
    private bool _disposed;
    private bool _manualPause;

    public bool IsReady => _running && !_disposed;
    public bool IsManuallyPaused => _manualPause;

    public WallpaperController(string wallpapersDirectory, string assetsDirectory,
        SettingsStore settings, Action<string> reportError)
    {
        _wallpapersDirectory = wallpapersDirectory;
        _assetsDirectory = assetsDirectory;
        _settings = settings;
        _reportError = reportError;
        _pollTimer = new System.Windows.Forms.Timer { Interval = 1600 };
        _pollTimer.Tick += PollTimerTick;
    }

    public async Task StartAsync()
    {
        string profile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nordwall", "WebView2");
        Directory.CreateDirectory(profile);
        _environment = await CoreWebView2Environment.CreateAsync(userDataFolder: profile);
        await ConfigureWindowsAsync();
        _running = true;
        _pollTimer.Start();
    }

    public async Task ApplySettingsAsync(DisplayPreferences newSettings)
    {
        _settings.Save(newSettings);

        if (!_running)
            return;

        await ConfigureWindowsAsync();
    }

    public async Task ToggleManualPauseAsync()
    {
        _manualPause = !_manualPause;
        await RefreshPauseAsync(Screen.AllScreens);
    }

    private async void PollTimerTick(object? sender, EventArgs args)
    {
        if (_disposed || _configuring || _checking)
            return;

        _checking = true;
        try
        {
            Screen[] screens = Screen.AllScreens;
            if (CalculateSignature(screens) != _displaySignature)
                await ConfigureWindowsAsync();
            else
                await RefreshPauseAsync(screens);
        }
        catch (Exception ex)
        {
            _reportError("Display monitoring: " + ex.Message);
        }
        finally
        {
            _checking = false;
        }
    }

    private async Task ConfigureWindowsAsync()
    {
        if (_disposed)
            return;
        if (_configuring)
        {
            _reconfigurePending = true;
            return;
        }

        _configuring = true;
        var prepared = new Dictionary<string, WallpaperForm>(StringComparer.OrdinalIgnoreCase);
        try
        {
            Screen[] screens = Screen.AllScreens;
            if (screens.Length == 0)
                throw new InvalidOperationException("Windows did not report any displays.");

            var targets = new List<(string Key, Rectangle Bounds)>();
            if (_settings.Current.Mode == WallpaperMode.Span)
                targets.Add((SpanKey, screens.Select(s => s.Bounds).Aggregate(Rectangle.Union)));
            else
                targets.AddRange(screens.Select(s => (s.DeviceName, s.Bounds)));

            if (_environment is null)
                throw new InvalidOperationException("The WebView2 environment is not ready.");

            foreach ((string key, Rectangle bounds) in targets)
            {
                WallpaperFile? wallpaper = PickWallpaper(key);
                if (wallpaper is null)
                    continue; // Keep the normal Windows wallpaper on this display.

                var view = new WallpaperForm(_wallpapersDirectory, _assetsDirectory,
                    bounds, _reportError);
                prepared.Add(key, view);

                if (!view.AttachToDesktop(out string failureReason))
                    throw new InvalidOperationException(
                        $"Could not attach {key} to the desktop: {failureReason}");

                view.Show();
                await view.InitializeBrowserAsync(_environment);

                view.Display(wallpaper);
            }

            foreach (WallpaperForm old in _views.Values)
            {
                old.Close();
                old.Dispose();
            }
            _views.Clear();
            foreach ((string key, WallpaperForm view) in prepared)
                _views[key] = view;
            prepared.Clear();

            _displaySignature = CalculateSignature(screens);
            await RefreshPauseAsync(screens);
        }
        catch (Exception ex)
        {
            foreach (WallpaperForm form in prepared.Values)
            {
                form.Close();
                form.Dispose();
            }

            if (!_running)
                throw;

            _reportError("Unable to update display layout: " + ex.Message);
        }
        finally
        {
            _configuring = false;
            if (_reconfigurePending && !_disposed)
            {
                _reconfigurePending = false;
                _ = ConfigureWindowsAsync();
            }
        }
    }

    private WallpaperFile? PickWallpaper(string key)
    {
        IReadOnlyList<WallpaperFile> files = WallpaperCatalog.GetAll(_wallpapersDirectory);
        if (files.Count == 0)
            return null;

        DisplayPreferences prefs = _settings.Current;
        string? name;
        if (key == SpanKey)
        {
            name = prefs.SpanWallpaper ?? prefs.LastWallpaper;
        }
        else
        {
            name = prefs.PerMonitorWallpapers.TryGetValue(key, out string? selected)
                ? selected : prefs.LastWallpaper;
        }

        // An unset or explicitly empty choice means "No wallpaper". Only the
        // old v0.3 LastWallpaper value is migrated automatically.
        if (string.IsNullOrEmpty(name))
            return null;

        return files.FirstOrDefault(file => file.Name.Equals(name,
            StringComparison.OrdinalIgnoreCase));
    }

    private async Task RefreshPauseAsync(Screen[] screens)
    {
        if (_disposed || _views.Count == 0)
            return;

        HashSet<string> fullscreen = _settings.Current.AutoPauseFullscreen
            ? FullscreenDetector.FindCoveredDisplays(screens)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach ((string key, WallpaperForm view) in _views)
        {
            bool autoPause = key == SpanKey
                ? fullscreen.Count > 0
                : fullscreen.Contains(key);
            await view.SetPlaybackPausedAsync(_manualPause || autoPause);
        }
    }

    private static string CalculateSignature(Screen[] screens) => string.Join("|",
        screens.OrderBy(s => s.DeviceName, StringComparer.OrdinalIgnoreCase)
            .Select(s => $"{s.DeviceName}:{s.Bounds.X},{s.Bounds.Y}," +
                         $"{s.Bounds.Width},{s.Bounds.Height}:{s.Primary}"));

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _pollTimer.Stop();
        _pollTimer.Dispose();
        foreach (WallpaperForm view in _views.Values)
        {
            view.Close();
            view.Dispose();
        }
        _views.Clear();
    }
}
