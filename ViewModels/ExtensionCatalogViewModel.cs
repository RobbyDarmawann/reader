using System.Collections.ObjectModel;
using ComicReader.Infrastructure.Extensions;

namespace ComicReader.ViewModels;

public sealed class ExtensionCatalogViewModel
{
    private readonly KeiyoushiRepositoryService _repositoryService;

    public ObservableCollection<KeiyoushiExtension> Extensions { get; } = new();

    public string RepositoryName { get; private set; } = "Keiyoushi";
    public string Status { get; private set; } = "Belum dimuat.";

    public ExtensionCatalogViewModel(KeiyoushiRepositoryService repositoryService)
    {
        _repositoryService = repositoryService;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Status = "Memuat extension...";
        Extensions.Clear();

        var repository = await _repositoryService.LoadAsync(
            KeiyoushiRepositoryService.DefaultIndexUrl,
            cancellationToken);

        RepositoryName = repository.Name;

        foreach (var extension in repository.Extensions
                     .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
        {
            Extensions.Add(extension);
        }

        Status = $"{Extensions.Count} extension ditemukan.";
    }
}
