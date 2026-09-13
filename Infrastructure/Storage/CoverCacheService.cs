using System.Security.Cryptography;
using System.Text;

namespace ComicReader.Infrastructure.Storage;

public sealed class CoverCacheService
{
    public async Task<string?> GetLocalPathAsync(
        string? url,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return null;

            var extension = Path.GetExtension(uri.AbsolutePath);
            if (string.IsNullOrWhiteSpace(extension) || extension.Length > 5)
                extension = ".jpg";

            var name = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(url))) + extension;
            var path = Path.Combine(LocalDataPaths.CoverCache, name);

            if (File.Exists(path))
                return path;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ComicReader/1.0");

            var bytes = await client.GetByteArrayAsync(url, cancellationToken);
            var temporaryPath = path + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
            File.Move(temporaryPath, path, true);
            return path;
        }
        catch
        {
            return url;
        }
    }
}