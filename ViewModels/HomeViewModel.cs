using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
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

    public HomeReadingItem(ReadingProgress progress)
    {
        Progress = progress;
        CoverUrl = progress.CoverUrl;
    }

    public void SetCover(string? coverUrl)
    {
        CoverUrl = coverUrl;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CoverUrl)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed class HomeViewModel : INotifyPropertyChanged
{
    private readonly ReadingProgressService _readingProgressService;
    private readonly CoverCacheService _coverCacheService = new();

    public ObservableCollection<HomeReadingItem> ContinueReading { get; } =
        new();

    public bool HasContinueReading =>
        ContinueReading.Count > 0;

    public event Action<ReadingProgress>? ResumeRequested;

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

                if (!string.IsNullOrWhiteSpace(progress.CoverUrl))
                {
                    var cached = await _coverCacheService.GetLocalPathAsync(progress.CoverUrl);
                    item.SetCover(cached);
                }
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

