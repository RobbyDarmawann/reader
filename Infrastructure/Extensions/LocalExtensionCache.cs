using System.Text.Json;
using ComicReader.Infrastructure.Storage;

namespace ComicReader.Infrastructure.Extensions;

public sealed record LocalExtensionCache(
    DateTimeOffset UpdatedAt,
    IReadOnlyList<KeiyoushiExtension> Extensions);

public sealed class LocalExtensionCacheService
{
    private const string FileName =
        "extensions.json";

    private readonly LocalJsonCache _cache =
        new();

    public async Task<IReadOnlyList<KeiyoushiExtension>>
        GetCachedAsync(
            CancellationToken cancellationToken = default)
    {
        var cached =
            await _cache.ReadAsync<LocalExtensionCache>(
                FileName,
                cancellationToken);

        return cached?.Extensions
            ?? Array.Empty<KeiyoushiExtension>();
    }

    public async Task SaveAsync(
        IReadOnlyList<KeiyoushiExtension> extensions,
        CancellationToken cancellationToken = default)
    {
        var data =
            new LocalExtensionCache(
                DateTimeOffset.UtcNow,
                extensions);

        await _cache.WriteAsync(
            FileName,
            data,
            cancellationToken);
    }

    public bool HasCache =>
        _cache.Exists(FileName);
}
