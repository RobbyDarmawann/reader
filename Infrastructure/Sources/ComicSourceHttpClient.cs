using System.Net;
using System.Net.Http.Headers;

namespace ComicReader.Infrastructure.Sources;

public sealed class ComicSourceHttpClient
{
    private readonly HttpClient _client;

    public ComicSourceHttpClient(HttpClient? client = null)
    {
        _client = client ?? CreateClient();
    }

    public async Task<string> GetStringAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        using var response = await _client.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(
            cancellationToken);
    }

    public async Task<byte[]> GetBytesAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        using var response = await _client.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(
            cancellationToken);
    }

    private static HttpClient CreateClient()
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
