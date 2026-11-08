using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Nordwall;

internal sealed class WallpaperForm : Form
{
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly string _wallpapersDirectory;
    private readonly string _assetsDirectory;
    private readonly Rectangle _displayBounds;
    private readonly WebView2 _browser;
    private readonly Action<string> _reportError;
    private string? _currentWallpaperName;
    private bool _paused;
    private int _pauseRevision;

    public WallpaperForm(string wallpapersDirectory, string assetsDirectory,
        Rectangle displayBounds, Action<string> reportError)
    {
        _wallpapersDirectory = wallpapersDirectory;
        _assetsDirectory = assetsDirectory;
        _displayBounds = displayBounds;
        _reportError = reportError;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        Bounds = displayBounds;

        _browser = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.Black
        };
        Controls.Add(_browser);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams parameters = base.CreateParams;
            parameters.ExStyle |= WsExToolWindow | WsExNoActivate;
            return parameters;
        }
    }

    public bool AttachToDesktop(out string failureReason)
    {
        // Accessing Handle creates the native window before it becomes visible.
        return DesktopLayer.TryAttach(Handle, _displayBounds, out failureReason);
    }

    public async Task InitializeBrowserAsync(CoreWebView2Environment environment)
    {
        await _browser.EnsureCoreWebView2Async(environment);

        CoreWebView2 core = _browser.CoreWebView2;
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;

        core.SetVirtualHostNameToFolderMapping(
            "wallpapers.example", _wallpapersDirectory, CoreWebView2HostResourceAccessKind.DenyCors);
        core.SetVirtualHostNameToFolderMapping(
            "appassets.example", _assetsDirectory,
            CoreWebView2HostResourceAccessKind.DenyCors);

        // User-selected HTML can use local files, but cannot silently contact
        // third-party domains or open additional browser windows.
        core.NavigationStarting += (_, args) =>
        {
            if (!IsAllowedResource(args.Uri))
                args.Cancel = true;
        };
        core.NewWindowRequested += (_, args) => args.Handled = true;
        core.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
        core.WebResourceRequested += (_, args) =>
        {
            if (IsAllowedResource(args.Request.Uri))
                return;

            args.Response = core.Environment.CreateWebResourceResponse(
                null, 403, "Network access disabled", "Content-Type: text/plain");
        };

        core.NavigationCompleted += (_, args) =>
        {
            if (!args.IsSuccess)
                _reportError($"Failed to load wallpaper ({args.WebErrorStatus}).");
        };
    }

    public void Display(WallpaperFile wallpaper)
    {
        if (_currentWallpaperName == wallpaper.Name)
            return;

        _currentWallpaperName = wallpaper.Name;
        if (_paused)
        {
            _paused = false;
            _pauseRevision++;
            _browser.CoreWebView2.Resume();
            _browser.Visible = true;
        }

        string localAddress = "https://wallpapers.example/" + Uri.EscapeDataString(wallpaper.Name);
        string address = wallpaper.Type == WallpaperType.Html
            ? localAddress
            : "https://appassets.example/video-player.html?src=" + Uri.EscapeDataString(localAddress);

        _browser.CoreWebView2.Navigate(address);
    }

    public void Blank()
    {
        _currentWallpaperName = null;
        _browser.CoreWebView2.Navigate("about:blank");
    }

    /// <summary>
    /// Hide and suspend the WebView2 renderer. Unlike navigating to about:blank,
    /// this preserves the video's playback position and HTML page state.
    /// </summary>
    public async Task SetPlaybackPausedAsync(bool pause)
    {
        if (IsDisposed || !_browser.IsHandleCreated || _browser.CoreWebView2 is null)
            return;

        if (_paused == pause)
            return;

        _paused = pause;
        int revision = ++_pauseRevision;
        if (!pause)
        {
            _browser.CoreWebView2.Resume();
            _browser.Visible = true;
            return;
        }

        // Older WebView2 versions require the controller to be invisible
        // before TrySuspendAsync; it also avoids rendering covered pixels.
        _browser.Visible = false;
        try
        {
            await _browser.CoreWebView2.TrySuspendAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException
                                   or System.Runtime.InteropServices.COMException
                                   or ObjectDisposedException)
        {
            // Hidden WebViews are still throttled if suspension is unavailable.
        }

        if (!IsDisposed && revision != _pauseRevision && !_paused)
        {
            _browser.CoreWebView2.Resume();
            _browser.Visible = true;
        }
    }

    private static bool IsAllowedResource(string address)
    {
        if (address == "about:blank")
            return true;

        return Uri.TryCreate(address, UriKind.Absolute, out Uri? uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && (uri.Host.Equals("wallpapers.example", StringComparison.OrdinalIgnoreCase)
                || uri.Host.Equals("appassets.example", StringComparison.OrdinalIgnoreCase));
    }

}
