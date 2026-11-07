using System.Diagnostics;
using System.Security;

namespace Nordwall;

internal sealed class WallpaperApp : ApplicationContext
{
    private readonly string _directory;
    private readonly SettingsStore _settings;
    private readonly WallpaperForm _window;
    private readonly ContextMenuStrip _menu;
    private readonly NotifyIcon _tray;
    private readonly Icon _appIcon;

    private WallpaperFile? _selected;
    private bool _isReady;
    private bool _isPaused;
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
        _tray.DoubleClick += (_, _) => OpenWallpapersDirectory();

        _window = new WallpaperForm(_directory, BundledContent.AssetsDirectory, ShowError);
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

            if (!_window.AttachToDesktop(out string layerError))
            {
                MessageBox.Show("Nordwall could not attach to the desktop. " + layerError +
                    "\n\nTry restarting File Explorer or Nordwall.",
                    "Nordwall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                ExitApp();
                return;
            }

            _window.Show();
            await _window.InitializeBrowserAsync();
            _isReady = true;

            IReadOnlyList<WallpaperFile> files = WallpaperCatalog.GetAll(_directory);
            WallpaperFile? initial = files.FirstOrDefault(file =>
                file.Name.Equals(_settings.LastWallpaper, StringComparison.OrdinalIgnoreCase))
                ?? files.FirstOrDefault();

            if (initial != null)
                SelectWallpaper(initial);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup error: {ex.Message}\n\n" +
                "Make sure Microsoft Edge WebView2 Runtime is installed, then restart Nordwall.",
                "Nordwall", MessageBoxButtons.OK, MessageBoxIcon.Error);
            ExitApp();
        }
    }

    private void SelectWallpaper(WallpaperFile file)
    {
        if (!_isReady)
            return;

        if (!File.Exists(file.FullPath))
        {
            ShowError("This file no longer exists. Reopen the tray menu to refresh the list.");
            return;
        }

        _selected = file;
        _settings.SaveSelection(file.Name);
        _isPaused = false;
        _window.Display(file);
        _tray.Text = "Nordwall — " + (file.Name.Length > 42 ? file.Name[..42] : file.Name);
    }

    private void TogglePause()
    {
        if (!_isReady || _selected is null)
            return;

        _isPaused = !_isPaused;
        if (_isPaused)
            _window.Blank();
        else
            _window.Display(_selected);
    }

    private void RebuildMenu()
    {
        while (_menu.Items.Count != 0)
        {
            ToolStripItem item = _menu.Items[0];
            _menu.Items.RemoveAt(0);
            item.Dispose();
        }

        _menu.Items.Add(new ToolStripMenuItem("Nordwall · Select wallpaper") { Enabled = false });
        _menu.Items.Add(new ToolStripSeparator());

        try
        {
            IReadOnlyList<WallpaperFile> files = WallpaperCatalog.GetAll(_directory);
            if (files.Count == 0)
                _menu.Items.Add(new ToolStripMenuItem("No HTML or MP4/WebM files found") { Enabled = false });

            foreach (WallpaperFile file in files)
            {
                string type = file.Type == WallpaperType.Html ? "⌘" : "▶";
                var item = new ToolStripMenuItem($"{type}  {file.Name}")
                {
                    Checked = _selected?.FullPath == file.FullPath,
                    Enabled = _isReady
                };
                item.Click += (_, _) => SelectWallpaper(file);
                _menu.Items.Add(item);
            }
        }
        catch (IOException ex)
        {
            _menu.Items.Add(new ToolStripMenuItem($"Cannot read wallpaper folder: {ex.Message}")
            {
                Enabled = false
            });
        }
        catch (UnauthorizedAccessException)
        {
            _menu.Items.Add(new ToolStripMenuItem("Access denied to the wallpapers folder")
            {
                Enabled = false
            });
        }

        _menu.Items.Add(new ToolStripSeparator());
        var pause = new ToolStripMenuItem(_isPaused ? "Resume" : "Pause")
        {
            Enabled = _isReady && _selected != null
        };
        pause.Click += (_, _) => TogglePause();
        _menu.Items.Add(pause);

        var launchAtSignIn = new ToolStripMenuItem("Launch at sign-in")
        {
            Checked = StartupManager.IsEnabled()
        };
        launchAtSignIn.Click += (_, _) => ToggleStartup();
        _menu.Items.Add(launchAtSignIn);

        var folder = new ToolStripMenuItem("Open wallpapers folder");
        folder.Click += (_, _) => OpenWallpapersDirectory();
        _menu.Items.Add(folder);

        var exit = new ToolStripMenuItem("Exit");
        exit.Click += (_, _) => ExitApp();
        _menu.Items.Add(exit);
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

    private void ToggleStartup()
    {
        try
        {
            StartupManager.SetEnabled(!StartupManager.IsEnabled());
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or SecurityException
                                   or IOException or InvalidOperationException)
        {
            MessageBox.Show($"Unable to change startup preference: {ex.Message}",
                "Nordwall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
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
        _tray.Visible = false;
        _tray.Dispose();
        _appIcon.Dispose();
        _menu.Dispose();
        _window.Close();
        _window.Dispose();
        ExitThread();
    }
}
