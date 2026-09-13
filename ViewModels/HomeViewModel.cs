using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using ComicReader.Core.Models;
using ComicReader.Infrastructure.Sources;
using ComicReader.Infrastructure.Reading;
using ComicReader.Infrastructure.Storage;

namespace ComicReader.ViewModels;

public sealed class HomeReadingItem : INotifyPropertyChanged
{
    public ReadingProgress Progress { get; }
    public string MangaTitle => Progress.MangaTitle;
    public string ChapterName => Progress.ChapterName;
    public DateTimeOffset UpdatedAt => Progress.UpdatedAt;
    public string? CoverUrl { get; private set; }
    public Bitmap? CoverImage { get; private set; }

    public HomeReadingItem(ReadingProgress progress)
    {
        Progress = progress;
        CoverUrl = progress.CoverUrl;
    }

    public async Task SetCoverAsync(string? coverUrl)
    {
        CoverUrl = coverUrl;

        if (!string.IsNullOrWhiteSpace(coverUrl) && File.Exists(coverUrl))
        {
            await using var stream = File.OpenRead(coverUrl);
            CoverImage = new Bitmap(stream);
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CoverImage)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class HomeViewModel : INotifyPropertyChanged
{
    private readonly ReadingProgressService _readingProgressService;
    private readonly CoverCacheService _coverCacheService = new();
    private readonly BacaKomikSource _legacySource = new();

    public ObservableCollection<HomeReadingItem> ContinueReading { get; } =
        new();

    public bool HasContinueReading =>
        ContinueReading.Count > 0;

    public event Action<ReadingProgress>? ResumeRequested;
    public event Action<ReadingProgress>? DetailRequested;

    public HomeViewModel()
    {
        _readingProgressService =
            new ReadingProgressService();

        _ = LoadLocalHistoryAsync();
    }

    public void Resume(HomeReadingItem item)
    {
        ResumeRequested?.Invoke(item.Progress with { CoverUrl = item.CoverUrl });
    }

    public void OpenDetail(HomeReadingItem item)
    {
        DetailRequested?.Invoke(item.Progress with { CoverUrl = item.CoverUrl });
    }

    private async Task LoadLocalHistoryAsync()
    {
        ContinueReading.Clear();

        try
        {
            foreach (var progress in
                     _readingProgressService.GetAllLatest()
                         .Take(20))
            {
                var item = new HomeReadingItem(progress);
                ContinueReading.Add(item);
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasContinueReading)));

                var coverUrl = progress.CoverUrl;
                if (string.IsNullOrWhiteSpace(coverUrl))
                {
                    try
                    {
                        var details = await _legacySource.GetDetailsAsync(
                            new Manga("bacakomik", progress.MangaUrl, progress.MangaTitle));
                        coverUrl = details?.CoverUrl;
                    }
                    catch
                    {
                    }
                }

                var cached = await _coverCacheService.GetLocalPathAsync(coverUrl);
                await item.SetCoverAsync(cached);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Gagal memuat Home history: {ex}");
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

