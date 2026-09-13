using System.Collections.ObjectModel;
using System.Threading.Tasks;

using ComicReader.Models;
using ComicReader.Services;

namespace ComicReader.ViewModels;

public class LibraryViewModel
{
    private readonly DatabaseService _databaseService;

    public ObservableCollection<Manga> MangaList { get; } = new();

    public LibraryViewModel()
    {
        _databaseService = new DatabaseService();

        _ = LoadLibraryAsync();
    }

    private async Task LoadLibraryAsync()
    {
        var mangaList = await _databaseService.GetLibraryAsync();

        MangaList.Clear();

        foreach (var manga in mangaList)
        {
            MangaList.Add(manga);
        }
    }
}