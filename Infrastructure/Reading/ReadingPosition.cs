namespace ComicReader.Infrastructure.Reading;

public sealed record ReadingPosition(
    string MangaUrl,
    string ChapterUrl,
    int PageIndex,
    double PageProgress,
    double ScrollRatio,
    DateTimeOffset UpdatedAt);
