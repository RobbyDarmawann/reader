namespace ComicReader.Core.Models;

public sealed record Manga(
    string SourceId,
    string Url,
    string Title,
    string? CoverUrl = null,
    string? Author = null,
    string? Artist = null,
    string? Description = null,
    string? Status = null,
    string? Type = null,
    IReadOnlyList<string>? Genres = null);

public sealed record Chapter(
    string SourceId,
    string Url,
    string Name,
    string? Scanlator = null,
    DateTimeOffset? PublishedAt = null,
    int Number = 0);

public sealed record Page(
    int Index,
    string ImageUrl,
    IReadOnlyDictionary<string, string>? Headers = null);

public sealed record ComicSourceInfo(
    string Id,
    string Name,
    string Language,
    string BaseUrl);
