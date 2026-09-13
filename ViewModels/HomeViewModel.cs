using System.Collections.ObjectModel;
using ComicReader.Infrastructure.Reading;

namespace ComicReader.ViewModels;

public sealed class HomeViewModel
{
    private readonly ReadingProgressService _readingProgressService;

    public ObservableCollection<ReadingProgress> ContinueReading { get; } =
        new();

    public bool HasContinueReading =>
        ContinueReading.Count > 0;

    public event Action<ReadingProgress>? ResumeRequested;

    public HomeViewModel()
    {
        _readingProgressService =
            new ReadingProgressService();

        LoadLocalHistory();
    }

    public void Resume(ReadingProgress progress)
    {
        ResumeRequested?.Invoke(progress);
    }

    private void LoadLocalHistory()
    {
        ContinueReading.Clear();

        try
        {
            foreach (var progress in
                     _readingProgressService.GetAllLatest()
                         .Take(20))
            {
                ContinueReading.Add(progress);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Gagal memuat Home history: {ex}");
        }
    }
}

