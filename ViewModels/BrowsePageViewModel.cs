using ComicReader.Core.Sources;
using System.Collections.ObjectModel;
using ComicReader.Infrastructure.Sources;

namespace ComicReader.ViewModels;

public sealed class BrowsePageViewModel
{
    public BrowseViewModel Extensions { get; }
    public SourceSearchViewModel Search { get; }

    public ObservableCollection<IComicSource> Sources { get; }

    public BrowsePageViewModel()
    {
        Extensions = new BrowseViewModel();
        Search = new SourceSearchViewModel();

        Sources = new ObservableCollection<IComicSource>(
            Search.Sources);
    }
}

