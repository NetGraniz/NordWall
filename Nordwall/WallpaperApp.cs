using System.Diagnostics;

namespace Nordwall;

internal sealed class WallpaperApp : ApplicationContext
{
    private readonly string _directory;
    private readonly SettingsStore _settings;
    private readonly WallpaperController _controller;
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _tray;
    private readonly Icon _appIcon;

    private SettingsForm? _settingsForm;
    private bool _isExiting;

    public WallpaperApp()
    {
        _directory = WallpaperCatalog.LocateDirectory();
        _settings = new SettingsStore();

        _menu = new ContextMenuStrip();
        _menu.Opening += (_, _) => RebuildMenu();

        _appIcon = LoadAppIcon();

        _tray = new NotifyIcon
        {
            Icon = _appIcon,
            Text = "Nordwall - animated wallpapers",
            ContextMenuStrip = _menu,
            Visible = true
        };
        _tray.DoubleClick += (_, _) => OpenSettings();

        _controller = new WallpaperController(_directory, BundledContent.AssetsDirectory,
            _settings, ShowError);
        Application.Idle += HandleFirstIdle;
    }

    private void HandleFirstIdle(object? sender, EventArgs args)
    {
        Application.Idle -= HandleFirstIdle;
        _ = StartAsync();
    }

    private async Task StartAsync()
    {
        try
        {
            BundledContent.Prepare(_directory);
            await _controller.StartAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup error: {ex.Message}\n\n" +
                "Make sure Microsoft Edge WebView2 Runtime is installed, then restart Nordwall.",
                "Nordwall", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ExitApp();
        }
    }

    private async void TogglePause()
    {
        if (!_controller.IsReady)
            return;
        try
        {
            await _controller.ToggleManualPauseAsync();
        }
        catch (Exception ex)
        {
            ShowError("Cannot change playback state: " + ex.Message);
        }
    }

    private void RebuildMenu()
    {
        while (_menu.Items.Count != 0)
        {
            ToolStripItem item = _menu.Items[0];
            _menu.Items.RemoveAt(0);
            item.Dispose();
        }

        var pause = new ToolStripMenuItem(_controller.IsManuallyPaused ? "Resume" : "Pause")
        {
            Enabled = _controller.IsReady
        };
        pause.Click += (_, _) => TogglePause();
        _menu.Items.Add(pause);

        var folder = new ToolStripMenuItem("Open wallpapers folder");
        folder.Click += (_, _) => OpenWallpapersDirectory();
        _menu.Items.Add(folder);

        var settings = new ToolStripMenuItem("Settings...");
        settings.Click += (_, _) => OpenSettings();
        _menu.Items.Add(settings);
    }

    private void OpenSettings()
    {
        if (_isExiting)
            return;

        if (_settingsForm is { IsDisposed: false })
        {
            _settingsForm.BringToFront();
            _settingsForm.Activate();
            return;
        }

        _settingsForm = new SettingsForm(_settings.Current, Screen.AllScreens,
            _directory, _appIcon, ApplySettings, ExitApp);
        _settingsForm.FormClosed += (_, _) => _settingsForm = null;
        _settingsForm.Show();
    }

    private async void ApplySettings(DisplayPreferences preferences)
    {
        try
        {
            await _controller.ApplySettingsAsync(preferences);
        }
        catch (Exception ex)
        {
            ShowError("Cannot apply wallpaper settings: " + ex.Message);
        }
    }

    private void OpenWallpapersDirectory()
    {
        Process.Start(new ProcessStartInfo(_directory) { UseShellExecute = true });
    }

    private static Icon LoadAppIcon()
    {
        // The executable has an embedded application icon. Prefer that for the
        // tray so it still works even if the separate ICO file is missing.
        try
        {
            Icon? embedded = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            if (embedded != null)
                return embedded;
        }
        catch (Exception ex) when (ex is ArgumentException or IOException
                                   or System.ComponentModel.Win32Exception)
        {
            // Fall back to the icon copied into the assets directory.
        }

        // The icon is also a managed embedded resource, so even the fallback
        // works when the published application consists of just one EXE.
        try
        {
            using Stream? iconData = typeof(WallpaperApp).Assembly.GetManifestResourceStream(
                "Nordwall.Bundled.Icon");
            if (iconData != null)
                return new Icon(iconData);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            // Fall back to a system icon if the resource cannot be decoded.
        }

        return (Icon)SystemIcons.Application.Clone();
    }

    private void ShowError(string text)
    {
        if (!_isExiting)
            _tray.ShowBalloonTip(4000, "Nordwall", text, ToolTipIcon.Warning);
    }

    private void ExitApp()
    {
        if (_isExiting) return;
        _isExiting = true;
        Application.Idle -= HandleFirstIdle;
        _settingsForm?.Close();
        _controller.Dispose();
        _tray.Visible = false;
        _tray.Dispose();
        _appIcon.Dispose();
        _menu.Dispose();
        ExitThread();
    }
}
