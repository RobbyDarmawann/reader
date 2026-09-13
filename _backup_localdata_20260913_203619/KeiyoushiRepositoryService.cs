using System.Net;
using System.Net.Http.Headers;

namespace ComicReader.Infrastructure.Extensions;

public sealed class KeiyoushiRepositoryService
{
    public const string DefaultIndexUrl =
        "https://github.com/keiyoushi/extensions/raw/repo/index.pb";

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

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return KeiyoushiIndexParser.Parse(bytes);
    }

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ComicReader", "0.1"));

        return client;
    }
}
