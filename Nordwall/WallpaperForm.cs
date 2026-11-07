using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;

namespace Nordwall;

internal sealed class WallpaperForm : Form
{
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;

    private readonly string _wallpapersDirectory;
    private readonly string _assetsDirectory;
    private readonly WebView2 _browser;
    private readonly Action<string> _reportError;

    public WallpaperForm(string wallpapersDirectory, string assetsDirectory, Action<string> reportError)
    {
        _wallpapersDirectory = wallpapersDirectory;
        _assetsDirectory = assetsDirectory;
        _reportError = reportError;

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        Bounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1920, 1080);

        _browser = new WebView2
        {
            Dock = DockStyle.Fill,
            DefaultBackgroundColor = Color.Black
        };
        Controls.Add(_browser);

        SystemEvents.DisplaySettingsChanged += HandleDisplaySettingsChanged;
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
        return DesktopLayer.TryAttach(Handle, Size, out failureReason);
    }

    public async Task InitializeBrowserAsync()
    {
        string profile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Nordwall", "WebView2");
        Directory.CreateDirectory(profile);

        CoreWebView2Environment environment = await CoreWebView2Environment.CreateAsync(
            userDataFolder: profile);
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
        string localAddress = "https://wallpapers.example/" + Uri.EscapeDataString(wallpaper.Name);
        string address = wallpaper.Type == WallpaperType.Html
            ? localAddress
            : "https://appassets.example/video-player.html?src=" + Uri.EscapeDataString(localAddress);

        _browser.CoreWebView2.Navigate(address);
    }

    public void Blank()
    {
        // An empty document stops video playback and JavaScript timers altogether.
        _browser.CoreWebView2.Navigate("about:blank");
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

    private void HandleDisplaySettingsChanged(object? sender, EventArgs args)
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        BeginInvoke((Action)(() =>
        {
            if (IsDisposed) return;
            Size size = Screen.PrimaryScreen?.Bounds.Size ?? Size;
            DesktopLayer.Resize(Handle, size);
            Size = size;
        }));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            SystemEvents.DisplaySettingsChanged -= HandleDisplaySettingsChanged;

        base.Dispose(disposing);
    }
}
