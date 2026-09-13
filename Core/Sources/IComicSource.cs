using ComicReader.Core.Models;

namespace ComicReader.Core.Sources;

public interface IComicSource
{
    string Id { get; }

    string Name { get; }

    string Language { get; }

    string BaseUrl { get; }

    Task<IReadOnlyList<Manga>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default);

    Task<Manga?> GetDetailsAsync(
        Manga manga,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Chapter>> GetChaptersAsync(
        Manga manga,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Page>> GetPagesAsync(
        Chapter chapter,
        CancellationToken cancellationToken = default);
}
