using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ComicReader.Core.Models;
using ComicReader.Core.Sources;
using ComicReader.Infrastructure.Sources;

namespace ComicReader.ViewModels;

public sealed class SourceSearchViewModel : INotifyPropertyChanged
{
    private readonly ComicSourceRegistry _registry;

    private string _query = string.Empty;
    private string _status = "Siap mencari manga.";
    private bool _isLoading;

    public ObservableCollection<Manga> Results { get; } = new();

    public IReadOnlyCollection<IComicSource> Sources =>
        _registry.Sources;

    public IComicSource? SelectedSource { get; set; }

    public string Query
    {
        get => _query;
        set
        {
            if (_query == value)
                return;

            _query = value;
            OnPropertyChanged();
        }
    }

    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value)
                return;

            _status = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (_isLoading == value)
                return;

            _isLoading = value;
            OnPropertyChanged();
        }
    }

    public SourceSearchViewModel()
    {
        _registry = new ComicSourceRegistry();

        // Runtime adapter BacaKomik
        _registry.Register(new BacaKomikSource());

        SelectedSource = _registry.Sources.FirstOrDefault();
    }

    public async Task SearchAsync()
    {
        var source = SelectedSource;

        if (source is null)
        {
            Status = "Belum ada source.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Query))
        {
            Status = "Masukkan judul manga.";
            Results.Clear();
            return;
        }

        if (IsLoading)
            return;

        IsLoading = true;
        Results.Clear();
        Status = $"Mencari \"{Query}\" di {source.Name}...";

        try
        {
            var results = await source.SearchAsync(Query.Trim());

            foreach (var manga in results)
                Results.Add(manga);

            Status = $"{Results.Count} hasil ditemukan.";
        }
        catch (Exception ex)
        {
            Status = $"Gagal mencari: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
