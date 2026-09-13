using ComicReader.Core.Models;

namespace ComicReader.Core.Sources;

public sealed record SourceCatalogFilter(
    string Id,
    string Name,
    IReadOnlyList<string> Options);

public sealed record SourceCatalogRequest(
    string Section = "popular",
    string? Genre = null,
    string? Type = null,
    string? Status = null,
    string? Format = null,
    string? Sort = null,
    int Page = 1);

public interface IComicCatalogSource
{
    IReadOnlyList<SourceCatalogFilter> Filters { get; }

    Task<IReadOnlyList<Manga>> GetCatalogAsync(
        SourceCatalogRequest request,
        CancellationToken cancellationToken = default);
}
