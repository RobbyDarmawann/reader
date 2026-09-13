namespace ComicReader.Infrastructure.Storage;

public static class LocalDataPaths
{
    public static string Root
    {
        get
        {
            var root = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "ComicReader");

            Directory.CreateDirectory(root);
            return root;
        }
    }

    public static string Database =>
        Path.Combine(Root, "comicreader.db");

    public static string Cache =>
        EnsureDirectory("cache");

    public static string ExtensionCache =>
        EnsureDirectory("cache", "extensions");

    public static string CoverCache =>
        EnsureDirectory("cache", "covers");

    public static string PageCache =>
        EnsureDirectory("cache", "pages");

    public static string Downloads =>
        EnsureDirectory("downloads");

    public static string ExtensionStorage =>
        EnsureDirectory("extensions");

    public static string Library =>
        EnsureDirectory("library");

    public static string History =>
        EnsureDirectory("history");

    public static string Temp =>
        EnsureDirectory("temp");

    private static string EnsureDirectory(
        params string[] parts)
    {
        var path = Root;

        foreach (var part in parts)
            path = Path.Combine(path, part);

        Directory.CreateDirectory(path);
        return path;
    }
}
