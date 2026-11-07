namespace Nordwall;

/// <summary>
/// Creates the empty user wallpaper folder and stages the internal video player
/// outside the application directory. No files are needed next to Nordwall.exe.
/// </summary>
internal static class BundledContent
{
    private const string ResourcePrefix = "Nordwall.Bundled.";

    public static string AssetsDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Nordwall", "assets");

    public static void Prepare(string wallpapersDirectory)
    {
        // Never install, restore, or modify user wallpapers. The folder starts
        // empty unless the user has already put files there.
        Directory.CreateDirectory(wallpapersDirectory);

        // WebView2's virtual-host mapping requires a real directory. This
        // tiny internal page is embedded in the EXE and re-staged on startup.
        Directory.CreateDirectory(AssetsDirectory);
        using Stream videoPlayer = OpenResource("VideoPlayer");
        using var cachedPage = new FileStream(
            Path.Combine(AssetsDirectory, "video-player.html"), FileMode.Create);
        videoPlayer.CopyTo(cachedPage);
    }

    private static Stream OpenResource(string suffix)
    {
        string resourceName = ResourcePrefix + suffix;
        return typeof(BundledContent).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Missing embedded resource: {resourceName}");
    }
}
