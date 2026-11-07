namespace Nordwall;

internal enum WallpaperType
{
    Html,
    Video
}

internal sealed record WallpaperFile(string Name, string FullPath, WallpaperType Type);

internal static class WallpaperCatalog
{
    private static readonly HashSet<string> HtmlExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".html", ".htm"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".webm"
    };

    public static IReadOnlyList<WallpaperFile> GetAll(string directory)
    {
        Directory.CreateDirectory(directory);

        return Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Select(path =>
            {
                string extension = Path.GetExtension(path);
                WallpaperType? type = null;
                if (HtmlExtensions.Contains(extension))
                    type = WallpaperType.Html;
                else if (VideoExtensions.Contains(extension))
                    type = WallpaperType.Video;

                return type is null ? null : new WallpaperFile(Path.GetFileName(path), path, type.Value);
            })
            .OfType<WallpaperFile>()
            .OrderBy(file => file.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public static string LocateDirectory()
    {
        // Respect Documents redirection (including OneDrive and localized names).
        // The folder is independent of where the single-file executable is kept.
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        if (string.IsNullOrWhiteSpace(documents))
            throw new InvalidOperationException("Windows could not locate the Documents folder.");

        return Path.Combine(documents, "Wallpapers");
    }
}
