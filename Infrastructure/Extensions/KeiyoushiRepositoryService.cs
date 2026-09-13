using System.Net;
using System.Net.Http.Headers;
using ComicReader.Infrastructure.Storage;

namespace ComicReader.Infrastructure.Extensions;

public sealed class KeiyoushiRepositoryService
{
    public const string DefaultIndexUrl =
        "https://github.com/keiyoushi/extensions/raw/repo/index.pb";

    private const string CacheFileName =
        "keiyoushi-index.pb";

    private readonly HttpClient _httpClient;

    public KeiyoushiRepositoryService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateHttpClient();
    }

    public async Task<KeiyoushiRepository> LoadAsync(
        string indexUrl = DefaultIndexUrl,
        CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(
            indexUrl,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var bytes =
            await response.Content.ReadAsByteArrayAsync(
                cancellationToken);

        await SaveCacheAsync(
            bytes,
            cancellationToken);

        return KeiyoushiIndexParser.Parse(bytes);
    }

    public async Task<KeiyoushiRepository?> LoadCachedAsync(
        CancellationToken cancellationToken = default)
    {
        var path =
            Path.Combine(
                LocalDataPaths.Cache,
                CacheFileName);

        if (!File.Exists(path))
            return null;

        try
        {
            var bytes =
                await File.ReadAllBytesAsync(
                    path,
                    cancellationToken);

            if (bytes.Length == 0)
                return null;

            return KeiyoushiIndexParser.Parse(bytes);
        }
        catch
        {
            return null;
        }
    }

    public bool HasCachedIndex
    {
        get
        {
            var path =
                Path.Combine(
                    LocalDataPaths.Cache,
                    CacheFileName);

            return File.Exists(path);
        }
    }

    private static async Task SaveCacheAsync(
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        var path =
            Path.Combine(
                LocalDataPaths.Cache,
                CacheFileName);

        var temporaryPath =
            path + ".tmp";

        await File.WriteAllBytesAsync(
            temporaryPath,
            bytes,
            cancellationToken);

        File.Move(
            temporaryPath,
            path,
            true);
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression =
                DecompressionMethods.GZip |
                DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue(
                "ComicReader",
                "0.1"));

        return client;
    }
}
