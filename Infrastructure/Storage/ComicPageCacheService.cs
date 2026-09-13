using System.Security.Cryptography;
using System.Text;
using ComicReader.Core.Models;

namespace ComicReader.Infrastructure.Storage;

public sealed class ComicPageCacheService
{
    public async Task<string?> GetLocalPathAsync(
        Page page,
        CancellationToken cancellationToken = default)
    {
        if (!Uri.TryCreate(page.ImageUrl, UriKind.Absolute, out _))
            return null;

        var name = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(page.ImageUrl))) + ".jpg";
        var path = Path.Combine(LocalDataPaths.PageCache, name);

        if (File.Exists(path))
            return path;

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ComicReader/1.0");

            if (page.Headers is not null)
            {
                foreach (var header in page.Headers)
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }

            var bytes = await client.GetByteArrayAsync(page.ImageUrl, cancellationToken);
            var temporaryPath = path + ".tmp";
            await File.WriteAllBytesAsync(temporaryPath, bytes, cancellationToken);
            File.Move(temporaryPath, path, true);
            return path;
        }
        catch
        {
            return null;
        }
    }
}