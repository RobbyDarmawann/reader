using ComicReader.Core.Models;
using ComicReader.Core.Sources;

namespace ComicReader.Infrastructure.Sources;

public sealed class UnsupportedExtensionSource : IComicSource
{
    public string Id { get; }

    public string Name { get; }

    public string Language { get; }

    public string BaseUrl { get; }

    public UnsupportedExtensionSource(
        ComicSourceDescriptor descriptor)
    {
        Id = descriptor.Id;
        Name = descriptor.Name;
        Language = descriptor.Language;
        BaseUrl = descriptor.BaseUrl;
    }
    public Task<Manga?> GetMangaAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"Extension source '{Name}' belum memiliki runtime adapter.");
    }
    public Task<IReadOnlyList<Manga>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"Extension source '{Name}' belum memiliki runtime adapter.");
    }

    public Task<Manga?> GetDetailsAsync(
        Manga manga,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"Extension source '{Name}' belum memiliki runtime adapter.");
    }

    public Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        Manga manga,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"Extension source '{Name}' belum memiliki runtime adapter.");
    }

    public Task<IReadOnlyList<Page>> GetPagesAsync(
        Chapter chapter,
        CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException(
            $"Extension source '{Name}' belum memiliki runtime adapter.");
    }
}

