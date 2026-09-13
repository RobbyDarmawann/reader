using ComicReader.Core.Models;
using ComicReader.Infrastructure.Storage;

namespace ComicReader.Infrastructure.Reading;

public sealed class ChapterDownloadService
{
    public async Task<string> DownloadAsync(
        Manga manga,
        Chapter chapter,
        IReadOnlyList<Page> pages,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var safeTitle = string.Concat(manga.Title.Select(c =>
            char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' ? c : '_'));
        var safeChapter = string.Concat(chapter.Name.Select(c =>
            char.IsLetterOrDigit(c) || c is ' ' or '-' or '_' ? c : '_'));
        var folder = Path.Combine(LocalDataPaths.Downloads, safeTitle, safeChapter);
        Directory.CreateDirectory(folder);

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ComicReader/1.0");

        for (var index = 0; index < pages.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.Combine(folder, $"{index + 1:000}.jpg");

            if (!File.Exists(path))
            {
                if (pages[index].Headers is not null)
                {
                    foreach (var header in pages[index].Headers)
                        client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                }

                var bytes = await client.GetByteArrayAsync(pages[index].ImageUrl, cancellationToken);
                await File.WriteAllBytesAsync(path, bytes, cancellationToken);
            }

            progress?.Report(index + 1);
        }

        return folder;
    }
}