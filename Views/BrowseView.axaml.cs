using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using ComicReader.Core.Models;
using ComicPage = ComicReader.Core.Models.Page;
using ComicReader.Core.Sources;
using ComicReader.Infrastructure.Extensions;
using ComicReader.Infrastructure.Sources;
using ComicReader.Infrastructure.Reading;
using ComicReader.Infrastructure.Storage;

namespace ComicReader.Views;

public partial class BrowseView : UserControl
{
    private readonly BrowseViewModel _viewModel;
    private readonly SourceSearchViewModel _searchViewModel;
    private readonly BacaKomikCatalogSource _bacaCatalog;
    private readonly BacaKomikSource _bacaSource;
    private IComicSource _activeSource;
    private readonly Dictionary<string, ComicSourceDescriptor> _sourceDescriptors =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ReadingProgressService _readingProgress;

    private Manga? _currentManga;
    private Chapter? _currentChapter;

    private readonly ReadingProgressService _readingProgressService =
        new ReadingProgressService();
    private readonly CoverCacheService _coverCacheService = new();
    private readonly ComicPageCacheService _pageCacheService = new();
    private readonly ChapterDownloadService _downloadService = new();

    private int _readerPageCount;
    private int _readerCurrentPage;
    private IReadOnlyList<Chapter> _readerChapters =
        Array.Empty<Chapter>();

    private CancellationTokenSource? _progressSaveTimer;
    private bool _isRestoringReadingProgress;
    private bool _returnToHomeAfterReader;
    private int _catalogPage = 1;
    private bool _catalogLoading;
    private bool _catalogHasMore = true;
    private int _sourceGeneration;
    public BrowseView()
    {
        InitializeComponent();

        _viewModel = new BrowseViewModel();
        _searchViewModel = new SourceSearchViewModel();
        _bacaCatalog = new BacaKomikCatalogSource();
        _bacaSource = new BacaKomikSource();
        _activeSource = _bacaSource;
        _readingProgress = new ReadingProgressService();

        BuildSections();
        BuildFilters();

        Loaded += BrowseView_Loaded;

        RefreshButton.Click += Refresh_Click;
        ExtensionSearchBox.TextChanged += ExtensionSearchBox_TextChanged;
        ExtensionLanguageComboBox.SelectionChanged += ExtensionLanguageComboBox_SelectionChanged;
        BackButton.Click += BackToExtensions_Click;
        SearchButton.Click += Search_Click;
        ApplyFilterButton.Click += ApplyFilter_Click;
        DetailBackButton.Click += DetailBackButton_Click;

        ReaderBackButton.Click += ReaderBackButton_Click;
        ResumeReadingButton.Click += ResumeReadingButton_Click;
        ReaderTopButton.Click += ReaderTopButton_Click;
        ReaderBottomButton.Click += ReaderBottomButton_Click;
        PreviousChapterButton.Click += PreviousChapterButton_Click;
        NextChapterButton.Click += NextChapterButton_Click;
        DownloadChapterButton.Click += DownloadChapterButton_Click;
        ReaderModeComboBox.ItemsSource = new[] { "Webtoon", "Horizontal" };
        ReaderModeComboBox.SelectedIndex = 0;
        ReaderModeComboBox.SelectionChanged += ReaderModeComboBox_SelectionChanged;
        ReaderScrollViewer.ScrollChanged += ReaderScrollViewer_ScrollChanged;
        MainScrollViewer.ScrollChanged += MainScrollViewer_ScrollChanged;
    }


    private async void BrowseView_Loaded(
        object? sender,
        RoutedEventArgs e)
    {
        var pendingResume =
            (DataContext as BrowseViewModel)?.TakePendingResume();
        var pendingDetail =
            (DataContext as BrowseViewModel)?.TakePendingDetail();

        // Extension loading berjalan di background agar tidak menghambat resume dari Home.
        _ = LoadExtensionsAsync();

        if (pendingResume is not null)
        {
            try
            {
                SelectSourceForUrl(pendingResume.MangaUrl);
                await OpenResumeAsync(
                    pendingResume);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Gagal membuka resume dari Home: {ex}");
            }
        }
        else if (pendingDetail is not null)
        {
            SelectSourceForUrl(pendingDetail.MangaUrl);
            await OpenMangaDetailAsync(new Manga(
                _activeSource.Id,
                pendingDetail.MangaUrl,
                pendingDetail.MangaTitle,
                pendingDetail.CoverUrl));
        }
    }

    private void SelectSourceForUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return;

        if (uri.Host.Contains("bacakomik", StringComparison.OrdinalIgnoreCase))
        {
            _activeSource = _bacaSource;
            return;
        }

        _activeSource = new NativeExtensionSource(
            new ComicSourceDescriptor(
                uri.Host,
                uri.Host,
                string.Empty,
                $"{uri.Scheme}://{uri.Host}",
                string.Empty,
                string.Empty));
    }


    private async Task LoadExtensionsAsync()
    {
        try
        {
            await _viewModel.LoadExtensionsAsync();

            BuildInstalledSources();
            BuildExtensions();
            ExtensionLanguageComboBox.ItemsSource = _viewModel.Languages;
            ExtensionLanguageComboBox.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            InstalledStatus.Text = $"Gagal memuat: {ex.Message}";
            ExtensionStatus.Text = ex.Message;
        }
    }

    private void ExtensionSearchBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        _viewModel.ExtensionQuery = ExtensionSearchBox.Text?.Trim() ?? string.Empty;
        BuildExtensions();
    }

    private void ExtensionLanguageComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ExtensionLanguageComboBox.SelectedItem is string language)
        {
            _viewModel.SelectedLanguage = language;
            BuildExtensions();
        }
    }


    private void BuildInstalledSources()
    {
        InstalledContainer.Children.Clear();
        _sourceDescriptors.Clear();

        var hasBacaKomik = _searchViewModel.Sources
            .Any(x => x.Id.Equals(
                "bacakomik",
                StringComparison.OrdinalIgnoreCase));

        var installedExtensions = _viewModel.Extensions
            .Where(x => _viewModel.InstalledPackages.Contains(
                x.PackageName))
            .ToArray();

        if (hasBacaKomik)
        {
            _sourceDescriptors["bacakomik"] = new ComicSourceDescriptor(
                "bacakomik", "BacaKomik", "id", "https://bacakomik.my", "native", "1.0");
            InstalledContainer.Children.Add(
                CreateSourceCard(
                    "bacakomik",
                    "BacaKomik",
                    "Indonesia • Native source adapter"));
        }

        foreach (var extension in installedExtensions)
        {
            foreach (var source in extension.Sources)
            {
                if (source.Name.Contains(
                        "BacaKomik",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                var sourceKey = CreateSourceKey(
                    extension.PackageName,
                    source.Id);

                _sourceDescriptors[sourceKey] = new ComicSourceDescriptor(
                    sourceKey,
                    source.Name,
                    source.Language,
                    source.BaseUrl,
                    extension.PackageName,
                    extension.VersionName);

                InstalledContainer.Children.Add(
                    CreateSourceCard(
                        sourceKey,
                        source.Name,
                        $"{source.Language} • {source.BaseUrl}"));
            }
        }

        InstalledStatus.Text =
            InstalledContainer.Children.Count == 0
                ? "Belum ada source."
                : $"{InstalledContainer.Children.Count} source tersedia.";
    }

    private static string CreateSourceKey(string packageName, ulong sourceId) =>
        $"{packageName}:{sourceId}";


    private Control CreateSourceCard(
        string sourceId,
        string title,
        string subtitle)
    {
        var button = new Button
        {
            HorizontalContentAlignment =
                Avalonia.Layout.HorizontalAlignment.Stretch,
            HorizontalAlignment =
                Avalonia.Layout.HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 8),
            Padding = new Thickness(16),
            Background = Avalonia.Media.Brushes.Transparent,
            CornerRadius = new CornerRadius(8)
        };

        var panel = new Grid
        {
            ColumnDefinitions =
                new ColumnDefinitions("*,Auto")
        };

        var text = new StackPanel();

        text.Children.Add(
            new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = Avalonia.Media.FontWeight.Bold
            });

        text.Children.Add(
            new TextBlock
            {
                Text = subtitle,
                FontSize = 13,
                Opacity = 0.6,
                Margin = new Thickness(0, 4, 0, 0)
            });

        panel.Children.Add(text);

        var arrow = new TextBlock
        {
            Text = "›",
            FontSize = 26,
            VerticalAlignment =
                Avalonia.Layout.VerticalAlignment.Center
        };

        Grid.SetColumn(arrow, 1);
        panel.Children.Add(arrow);

        button.Content = panel;

        button.Click += async (_, _) =>
            await OpenSourceAsync(sourceId, title, subtitle);

        return button;
    }


    private void BuildExtensions()
    {
        ExtensionContainer.Children.Clear();

        foreach (var extension in _viewModel.FilteredExtensions)
        {
            var border = new Border
            {
                Background = Avalonia.Media.Brushes.Transparent,
                Padding = new Thickness(16),
                Margin = new Thickness(0, 0, 0, 10),
                CornerRadius = new CornerRadius(8)
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                    new ColumnDefinitions("*,Auto")
            };

            var text = new StackPanel();

            text.Children.Add(
                new TextBlock
                {
                    Text = extension.Name,
                    FontSize = 17,
                    FontWeight = Avalonia.Media.FontWeight.Bold
                });

            text.Children.Add(
                new TextBlock
                {
                    Text =
                        $"{extension.VersionName} • {extension.Sources.Count} source",
                    FontSize = 13,
                    Opacity = 0.65,
                    Margin = new Thickness(0, 5, 0, 0)
                });

            grid.Children.Add(text);

            var install = new Button
            {
                Content =
                    _viewModel.InstalledPackages.Contains(
                        extension.PackageName)
                        ? "Uninstall"
                        : "Install",
                MinWidth = 104,
                Height = 36,
                Margin = new Thickness(16, 0, 0, 0)
            };

            Grid.SetColumn(install, 1);
            grid.Children.Add(install);

            install.Click += async (_, _) =>
            {
                try
                {
                    install.IsEnabled = false;

                    if (_viewModel.InstalledPackages.Contains(extension.PackageName))
                    {
                        await _viewModel.UninstallAsync(extension);
                        install.Content = "Install";
                    }
                    else
                    {
                        await _viewModel.InstallAsync(extension);
                        install.Content = "Uninstall";
                    }

                    BuildInstalledSources();
                }
                catch (Exception ex)
                {
                    ExtensionStatus.Text =
                        $"Install gagal: {ex.Message}";

                    install.IsEnabled = true;
                }
            };

            border.Child = grid;
            ExtensionContainer.Children.Add(border);
        }

        ExtensionStatus.Text =
            $"{_viewModel.FilteredExtensions.Count} extension.";
    }


    private async Task OpenSourceAsync(
        string sourceId,
        string title,
        string subtitle)
    {
        var generation = ++_sourceGeneration;
        ComicSourceDescriptor? descriptor = null;

        _activeSource = sourceId.Equals(
                "bacakomik",
                StringComparison.OrdinalIgnoreCase)
            ? _bacaSource
            : _sourceDescriptors.TryGetValue(sourceId, out descriptor)
                ? new NativeExtensionSource(descriptor)
                : new UnsupportedExtensionSource(new ComicSourceDescriptor(
                    sourceId, title, string.Empty, string.Empty, string.Empty, string.Empty));

        if (!sourceId.Equals(
                "bacakomik",
                StringComparison.OrdinalIgnoreCase))
        {
            ExtensionPage.IsVisible = false;
            SourcePage.IsVisible = true;
            MangaDetailPage.IsVisible = false;
            ReaderPage.IsVisible = false;

            SourceTitle.Text = title;
            SourceDescription.Text =
                _activeSource is NativeExtensionSource
                    ? $"{descriptor?.BaseUrl ?? title} • native adapter aktif"
                    : "Source terdeteksi, tetapi metadata URL belum tersedia.";

            SearchBox.IsEnabled = _activeSource is NativeExtensionSource;
            SearchButton.IsEnabled = _activeSource is NativeExtensionSource;
            FilterBorder.IsVisible = false;
            MangaGrid.Items.Clear();

            CatalogStatus.Text =
                _activeSource is NativeExtensionSource
                    ? "Ketik judul lalu tekan Cari untuk memuat komik dari source ini."
                    : "Source belum memiliki adapter yang bisa dipakai.";

            return;
        }

        ExtensionPage.IsVisible = false;
        SourcePage.IsVisible = true;
        MangaDetailPage.IsVisible = false;
        ReaderPage.IsVisible = false;

        SourceTitle.Text = "BacaKomik";
        SourceDescription.Text =
            "Popular, latest, search, dan katalog manga.";

        SearchBox.IsEnabled = true;
        SearchButton.IsEnabled = true;
        FilterBorder.IsVisible = true;

        BuildSections();
        BuildFilters();

        await LoadCatalogAsync();
    }


    private void BackToExtensions_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ReaderPage.IsVisible = false;
        MangaDetailPage.IsVisible = false;
        SourcePage.IsVisible = false;
        ExtensionPage.IsVisible = true;

        MangaGrid.Items.Clear();
    }


    private void BuildSections()
    {
        SectionContainer.Children.Clear();

        AddSectionButton("Popular", "popular");
        AddSectionButton("Latest", "latest");
    }


    private void AddSectionButton(
        string title,
        string section)
    {
        var button = new Button
        {
            Content = title,
            Classes = { "section-button" },
            Padding = new Thickness(16, 8),
            Margin = new Thickness(0, 0, 5, 0)
        };

        button.Click += async (_, _) =>
        {
            _currentSection = section;
            await LoadCatalogAsync();
        };

        SectionContainer.Children.Add(button);
    }


    private string _currentSection = "popular";


    private void BuildFilters()
    {
        var filters = _bacaCatalog.Filters;

        var genre = filters.FirstOrDefault(
            x => x.Id == "genre");

        var type = filters.FirstOrDefault(
            x => x.Id == "type");

        var status = filters.FirstOrDefault(
            x => x.Id == "status");

        var format = filters.FirstOrDefault(
            x => x.Id == "format");

        var sort = filters.FirstOrDefault(
            x => x.Id == "sort");

        GenreComboBox.ItemsSource =
            genre?.Options ?? [];

        TypeComboBox.ItemsSource =
            type?.Options ?? [];

        StatusComboBox.ItemsSource =
            status?.Options ?? [];

        FormatComboBox.ItemsSource =
            format?.Options ?? [];

        SortComboBox.ItemsSource =
            sort?.Options ?? [];

        if (GenreComboBox.ItemCount > 0)
            GenreComboBox.SelectedIndex = 0;

        if (TypeComboBox.ItemCount > 0)
            TypeComboBox.SelectedIndex = 0;

        if (StatusComboBox.ItemCount > 0)
            StatusComboBox.SelectedIndex = 0;

        if (FormatComboBox.ItemCount > 0)
            FormatComboBox.SelectedIndex = 0;

        if (SortComboBox.ItemCount > 0)
            SortComboBox.SelectedIndex = 0;
    }


    private async void ApplyFilter_Click(
        object? sender,
        RoutedEventArgs e)
    {
        await LoadCatalogAsync();
    }

    private async void MainScrollViewer_ScrollChanged(
        object? sender,
        ScrollChangedEventArgs e)
    {
        if (!_catalogLoading &&
            _catalogHasMore &&
            SourcePage.IsVisible &&
            MainScrollViewer.Offset.Y + MainScrollViewer.Viewport.Height >=
            MainScrollViewer.Extent.Height - 450)
        {
            await LoadCatalogAsync(append: true);
        }
    }


    private async Task LoadCatalogAsync(bool append = false)
    {
        if (_activeSource is not BacaKomikSource)
            return;

        var generation = _sourceGeneration;

        if (_catalogLoading)
            return;

        if (!append)
        {
            _catalogPage = 1;
            _catalogHasMore = true;
        }

        _catalogLoading = true;
        try
        {
            CatalogStatus.Text = append
                ? "Memuat komik berikutnya..."
                : "Memuat katalog...";

            var request = new SourceCatalogRequest(
                Section: _currentSection,
                Genre: GetFilterValue(GenreComboBox),
                Type: GetComboValue(TypeComboBox),
                Status: GetComboValue(StatusComboBox),
                Format: GetComboValue(FormatComboBox),
                Sort: GetComboValue(SortComboBox),
                Page: _catalogPage);

            var results =
                await _bacaCatalog.GetCatalogAsync(request);

            if (generation != _sourceGeneration ||
                _activeSource is not BacaKomikSource)
                return;

            if (append)
                AppendMangaGrid(results);
            else
                RenderMangaGrid(results);

            _catalogHasMore = results.Count > 0;
            _catalogPage++;

            CatalogStatus.Text =
                append
                    ? $"{MangaGrid.Items.Count} manga tersedia."
                    : $"{results.Count} manga ditemukan. Scroll untuk memuat lebih banyak.";
        }
        catch (Exception ex)
        {
            CatalogStatus.Text =
                $"Gagal memuat katalog: {ex.Message}";
        }
        finally
        {
            _catalogLoading = false;
        }
    }


    private async void Search_Click(
        object? sender,
        RoutedEventArgs e)
    {
        var query = SearchBox.Text?.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            CatalogStatus.Text =
                "Masukkan judul manga.";
            return;
        }

        try
        {
            CatalogStatus.Text =
                $"Mencari {query}...";

            var source = _activeSource;
            var results =
                await source.SearchAsync(query);

            if (!ReferenceEquals(source, _activeSource))
                return;

            RenderMangaGrid(results);
            _catalogHasMore = false;

            CatalogStatus.Text =
                $"{results.Count} hasil ditemukan.";
        }
        catch (Exception ex)
        {
            CatalogStatus.Text =
                $"Search gagal: {ex.Message}";
        }
    }


    private static string? GetComboValue(
        ComboBox combo)
    {
        return combo.SelectedItem?.ToString();
    }

    private static string? GetFilterValue(Control control)
    {
        if (control is ListBox list)
        {
            var selected = list.SelectedItems?
                .OfType<string>()
                .Where(x => !string.Equals(x, "All", StringComparison.OrdinalIgnoreCase));

            return selected is null ? null : string.Join(",", selected);
        }

        return (control as ComboBox)?.SelectedItem?.ToString();
    }


    private void RenderMangaGrid(
        IReadOnlyList<Manga> mangas)
    {
        MangaGrid.Items.Clear();

        foreach (var manga in mangas)
        {
            MangaGrid.Items.Add(
                CreateMangaCard(manga));
        }
    }

    private void AppendMangaGrid(IReadOnlyList<Manga> mangas)
    {
        var existingUrls = MangaGrid.Items
            .OfType<Control>()
            .Select(control => control.Tag as string)
            .Where(url => !string.IsNullOrWhiteSpace(url))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var manga in mangas)
        {
            if (existingUrls.Add(manga.Url))
                MangaGrid.Items.Add(CreateMangaCard(manga));
        }
    }


    private Control CreateMangaCard(
        Manga manga)
    {
        var border = new Border
        {
            Classes = { "manga-card" },
            Margin = new Thickness(6),
            Padding = new Thickness(8),
            Width = 165,
            Height = 318,
            CornerRadius = new CornerRadius(10),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        border.Tag = manga.Url;

        var stack = new StackPanel();

        var image = new Image
        {
            Width = 145,
            Height = 210,
            Stretch = Avalonia.Media.Stretch.UniformToFill,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        stack.Children.Add(image);

        _ = LoadCoverAsync(image, manga.CoverUrl);

        stack.Children.Add(
            new TextBlock
            {
                Text = manga.Title,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Height = 48,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                Margin = new Thickness(0, 8, 0, 6),
                FontWeight = Avalonia.Media.FontWeight.Bold,
                FontSize = 14
            });

        var open = new Button
        {
            Content = "Open",
            Height = 34,
            HorizontalAlignment =
                Avalonia.Layout.HorizontalAlignment.Stretch
        };

        open.Click += async (_, _) =>
            await OpenMangaDetailAsync(manga);

        stack.Children.Add(open);

        border.Child = stack;

        return border;
    }


    public async Task OpenResumeAsync(
        ReadingProgress progress)
    {
        // ====================================================
        // RESUME LANGSUNG DARI DATA LOKAL
        // ====================================================

        var manga =
            new Manga(
                _activeSource.Id,
                progress.MangaUrl,
                progress.MangaTitle,
                progress.CoverUrl);

        _returnToHomeAfterReader = true;

        var chapter =
            new Chapter(
                _activeSource.Id,
                progress.ChapterUrl,
                progress.ChapterName);

        _currentManga = manga;
        _currentChapter = chapter;

        _readerChapters =
            new[] { chapter };

        // Langsung tampilkan Reader.
        ExtensionPage.IsVisible = false;
        SourcePage.IsVisible = false;
        MangaDetailPage.IsVisible = false;
        ReaderPage.IsVisible = true;

        MainScrollViewer.IsVisible = false;

        ReaderTitle.Text =
            progress.MangaTitle;

        ReaderChapter.Text =
            progress.ChapterName;

        // ====================================================
        // TAMPILKAN PROGRESS LAMA SEBELUM GAMBAR DIMUAT
        // ====================================================

        var savedRatio =
            progress.ScrollMaximum > 0
                ? Math.Clamp(
                    progress.ScrollOffset /
                    progress.ScrollMaximum,
                    0,
                    1)
                : 0;

        var savedPercentage =
            savedRatio * 100.0;

        ReaderProgress.Text =
            $"Melanjutkan • {savedPercentage:0}%";

        ReaderPageContainer.Children.Clear();

        _isRestoringReadingProgress = true;

        try
        {
            // Ambil daftar halaman.
            var pages =
                await _activeSource.GetPagesAsync(
                    chapter);

            _readerPageCount =
                pages.Count;

            if (pages.Count == 0)
            {
                ReaderProgress.Text =
                    $"Tidak ada halaman • {savedPercentage:0}%";

                return;
            }

            // Tentukan halaman yang paling dekat
            // dengan progress tersimpan.
            var targetPageIndex =
                Math.Clamp(
                    (int)Math.Round(
                        savedRatio *
                        Math.Max(
                            0,
                            pages.Count - 1)),
                    0,
                    pages.Count - 1);

            _readerCurrentPage =
                targetPageIndex + 1;

            var images =
                new Image[pages.Count];

            var loadTasks =
                new Task[pages.Count];

            // Buat container gambar.
            for (var i = 0; i < pages.Count; i++)
            {
                var image =
                    new Image
                    {
                        Stretch =
                            Avalonia.Media.Stretch.Uniform,

                        HorizontalAlignment =
                            Avalonia.Layout.HorizontalAlignment.Center,

                        MaxWidth = 1000
                    };

                images[i] = image;

                ReaderPageContainer.Children.Add(
                    image);
            }

            // =================================================
            // PRIORITAS GAMBAR TARGET
            // =================================================

            try
            {
                loadTasks[targetPageIndex] =
                    LoadPageImageAsync(
                        images[targetPageIndex],
                        pages[targetPageIndex]);

                await loadTasks[targetPageIndex];
            }
            catch
            {
            }

            // =================================================
            // RENDER SEBELUM RESTORE SCROLL
            // =================================================

            await Avalonia.Threading.Dispatcher.UIThread
                .InvokeAsync(
                    () => { },
                    Avalonia.Threading.DispatcherPriority.Render);

            // =================================================
            // LANGSUNG PINDAH KE POSISI LAMA
            // =================================================

            await Avalonia.Threading.Dispatcher.UIThread
                .InvokeAsync(
                    () =>
                    {
                        var maximum =
                            ReaderScrollViewer.Extent.Height -
                            ReaderScrollViewer.Viewport.Height;

                        if (maximum > 0)
                        {
                            var targetOffset =
                                Math.Clamp(
                                    savedRatio * maximum,
                                    0,
                                    maximum);

                            ReaderScrollViewer.Offset =
                                new Vector(
                                    ReaderScrollViewer.Offset.X,
                                    targetOffset);
                        }

                        ReaderProgress.Text =
                            $"Melanjutkan • {savedPercentage:0}%";
                    },
                    Avalonia.Threading.DispatcherPriority.Render);

            // =================================================
            // SETELAH TARGET BERHASIL:
            // LOAD GAMBAR LAINNYA
            // =================================================

            for (var i = 0; i < pages.Count; i++)
            {
                if (i == targetPageIndex)
                    continue;

                var index = i;

                loadTasks[index] =
                    LoadPageImageAsync(
                        images[index],
                        pages[index]);
            }

            // =================================================
            // TUNGGU SEMUA GAMBAR SELESAI
            // =================================================
            //
            // Gambar target sudah ditampilkan lebih dahulu,
            // sehingga resume tetap terasa cepat.
            //
            // Setelah semua gambar selesai, tinggi total reader
            // sudah stabil. Baru posisi final dipasang kembali.
            //

            try
            {
                await Task.WhenAll(loadTasks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Sebagian halaman gagal dimuat: {ex}");
            }

            // =================================================
            // RESTORE POSISI FINAL
            // =================================================

            await Avalonia.Threading.Dispatcher.UIThread
                .InvokeAsync(
                    () => { },
                    Avalonia.Threading.DispatcherPriority.Render);

            var finalMaximum =
                ReaderScrollViewer.Extent.Height -
                ReaderScrollViewer.Viewport.Height;

            if (finalMaximum > 0)
            {
                var finalOffset =
                    Math.Clamp(
                        savedRatio * finalMaximum,
                        0,
                        finalMaximum);

                ReaderScrollViewer.Offset =
                    new Vector(
                        ReaderScrollViewer.Offset.X,
                        finalOffset);
            }

            // Pertahankan persentase yang berasal dari SQLite.
            ReaderProgress.Text =
                $"Melanjutkan • {savedPercentage:0}%";
        }
        catch (Exception ex)
        {
            ReaderProgress.Text =
                $"Gagal memuat • {savedPercentage:0}%";

            System.Diagnostics.Debug.WriteLine(
                $"Fast resume gagal: {ex}");
        }
        finally
        {
            _isRestoringReadingProgress = false;
        }
    }
    private async Task OpenMangaDetailAsync(
        Manga manga)
    {
        _currentManga = manga;

        ExtensionPage.IsVisible = false;
        SourcePage.IsVisible = false;
        MangaDetailPage.IsVisible = true;
        ReaderPage.IsVisible = false;

        MainScrollViewer.ScrollToHome();

        DetailTitle.Text = manga.Title;
        DetailSource.Text = _activeSource.Name;

        DetailAuthor.Text =
            $"Author: {manga.Author ?? "Tidak diketahui"}";

        DetailArtist.Text =
            $"Artist: {manga.Artist ?? "Tidak diketahui"}";

        DetailStatus.Text = "";
        DetailType.Text = "";
        DetailGenres.Text = "";

        DetailDescription.Text =
            "Memuat synopsis...";
        DownloadChapterButton.IsEnabled = false;
        DownloadChapterButton.Content = "Download chapter ini";

        DetailCover.Source = null;

        if (!string.IsNullOrWhiteSpace(manga.CoverUrl))
        {
            await LoadCoverAsync(
                DetailCover,
                manga.CoverUrl);
        }

        try
        {
            var details =
                await _activeSource.GetDetailsAsync(manga);

            if (details is not null)
            {
                DetailTitle.Text = details.Title;

                DetailAuthor.Text =
                    $"Author: {details.Author ?? "Tidak diketahui"}";

                DetailArtist.Text =
                    $"Artist: {details.Artist ?? "Tidak diketahui"}";

                DetailStatus.Text = $"Status: {details.Status ?? "Tidak diketahui"}";
                DetailType.Text = $"Type: {details.Type ?? "Tidak diketahui"}";
                DetailGenres.Text =
                    $"Genre: {(details.Genres is { Count: > 0 } ? string.Join(", ", details.Genres) : "Tidak diketahui")}";

                DetailDescription.Text =
                    string.IsNullOrWhiteSpace(details.Description)
                        ? "Synopsis tidak ditemukan."
                        : details.Description;

                if (!string.IsNullOrWhiteSpace(details.CoverUrl))
                {
                    await LoadCoverAsync(
                        DetailCover,
                        details.CoverUrl);
                }
            }

            ChapterStatus.Text =
                "Memuat chapter...";

            var chapters =
                await _activeSource.GetChaptersAsync(manga);

            _readerChapters = chapters;

            RenderChapters(chapters);

            ChapterStatus.Text =
                chapters.Count == 0
                    ? "Tidak ada chapter."
                    : $"{chapters.Count} chapter tersedia.";

            await UpdateResumeReadingButtonAsync();
            DownloadChapterButton.IsEnabled = chapters.Count > 0;
            DownloadChapterButton.Content = "Download chapter terbaru";
        }
        catch (Exception ex)
        {
            DetailDescription.Text =
                $"Gagal memuat detail: {ex.Message}";

            ChapterStatus.Text =
                $"Gagal memuat chapter: {ex.Message}";
        }
    }


    private void RenderChapters(
        IReadOnlyList<Chapter> chapters)
    {
        ChapterContainer.Children.Clear();

        foreach (var chapter in chapters)
        {
            var button = new Button
            {
                Content = chapter.Name,
                HorizontalContentAlignment =
                    Avalonia.Layout.HorizontalAlignment.Left,
                HorizontalAlignment =
                    Avalonia.Layout.HorizontalAlignment.Stretch,
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 7)
            };

            button.Click += async (_, _) =>
                await OpenChapterAsync(chapter);

            ChapterContainer.Children.Add(button);
        }
    }


    private async void ResumeReadingButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (_currentManga is null)
            return;

        var progress =
            _readingProgressService.GetLatest(
                _currentManga.Url);

        if (progress is null)
        {
            if (_readerChapters.Count == 0)
                return;

            var first =
                _readerChapters
                    .OrderBy(x => x.Number)
                    .First();

            await OpenChapterAsync(first);
            return;
        }

        var chapter =
            _readerChapters.FirstOrDefault(
                x => string.Equals(
                    x.Url,
                    progress.ChapterUrl,
                    StringComparison.OrdinalIgnoreCase));

        if (chapter is null)
            return;

        await OpenChapterAsync(chapter);
    }

    private async Task UpdateResumeReadingButtonAsync()
    {
        if (_currentManga is null)
        {
            ResumeReadingButton.IsVisible = false;
            return;
        }

        var progress =
            _readingProgressService.GetLatest(
                _currentManga.Url);

        if (progress is null)
        {
            ResumeReadingButton.Content = "▶ Mulai Baca";
        }
        else
        {
            var percent =
                progress.ScrollMaximum <= 0
                    ? 0
                    : progress.ScrollOffset /
                      progress.ScrollMaximum * 100;

            ResumeReadingButton.Content =
                $"▶ Lanjut Baca • {progress.ChapterName} • {percent:0}%";
        }

        ResumeReadingButton.IsVisible = true;
    }

    private async Task OpenChapterAsync(
        Chapter chapter)
    {
        _currentChapter = chapter;
        DownloadChapterButton.IsEnabled = true;
        DownloadChapterButton.Content = "Download chapter ini";

        ReaderPage.IsVisible = true;
        ExtensionPage.IsVisible = false;
        SourcePage.IsVisible = false;
        MangaDetailPage.IsVisible = false;

        MainScrollViewer.IsVisible = false;

        ReaderTitle.Text =
            _currentManga?.Title ?? "Reader";

        ReaderChapter.Text =
            chapter.Name;

        ReaderNavigationStatus.Text =
            $"Chapter {_readerChapters.ToList().FindIndex(x => x.Url == chapter.Url) + 1}/{_readerChapters.Count}";

        // ====================================================
        // BACA PROGRESS LOCAL TERLEBIH DAHULU
        // ====================================================

        ReadingProgress? saved = null;

        if (_currentManga is not null)
        {
            try
            {
                saved = _readingProgressService.Get(
                    _currentManga.Url,
                    chapter.Url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Gagal membaca progress chapter: {ex}");
            }
        }

        var savedRatio =
            saved is not null &&
            saved.ScrollMaximum > 0
                ? Math.Clamp(
                    saved.ScrollOffset /
                    saved.ScrollMaximum,
                    0,
                    1)
                : 0;

        var savedPercentage =
            savedRatio * 100.0;

        // Tampilkan progress lama sebelum request halaman.
        ReaderProgress.Text =
            saved is not null
                ? $"Melanjutkan • {savedPercentage:0}%"
                : "Memuat...";

        ReaderPageContainer.Children.Clear();

        _isRestoringReadingProgress = true;

        try
        {
            var pages =
                await _activeSource.GetPagesAsync(chapter);

            _readerPageCount = pages.Count;
            _readerCurrentPage = pages.Count > 0 ? 1 : 0;

            if (pages.Count == 0)
            {
                ReaderProgress.Text =
                    "Tidak ada halaman.";

                return;
            }

            // Tentukan halaman target dari progress terakhir.
            var targetPageIndex =
                saved is null
                    ? 0
                    : Math.Clamp(
                        (int)Math.Round(
                            savedRatio *
                            Math.Max(0, pages.Count - 1)),
                        0,
                        pages.Count - 1);

            var images =
                new Image[pages.Count];

            var loadTasks =
                new Task[pages.Count];

            // Buat semua Image control dahulu supaya struktur
            // reader sudah lengkap.
            for (var i = 0; i < pages.Count; i++)
            {
                var image = new Image
                {
                    Stretch =
                        Avalonia.Media.Stretch.Uniform,
                    HorizontalAlignment =
                        Avalonia.Layout.HorizontalAlignment.Center,
                    MaxWidth = 1000
                };

                images[i] = image;
                ReaderPageContainer.Children.Add(image);
            }

            ReaderProgress.Text =
                saved is null
                    ? $"Memuat 1 / {pages.Count}"
                    : $"Memulihkan halaman {targetPageIndex + 1} / {pages.Count}...";

            // Semua gambar mulai dimuat bersamaan.
            for (var i = 0; i < pages.Count; i++)
            {
                var index = i;

                loadTasks[index] =
                    LoadPageImageAsync(
                        images[index],
                        pages[index]);
            }

            // Untuk resume, tunggu HANYA gambar target.
            // Jadi tidak perlu menunggu seluruh chapter.
            if (saved is not null)
            {
                try
                {
                    await loadTasks[targetPageIndex];

                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
                        () =>
                        {
                            images[targetPageIndex]
                                .BringIntoView();

                            _readerCurrentPage =
                                targetPageIndex + 1;

                            ReaderProgress.Text =
                                $"Melanjutkan • " +
                                $"Page {_readerCurrentPage}/{pages.Count}";
                        });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"Gagal fast resume: {ex}");
                }
            }
            else
            {
                try
                {
                    await loadTasks[0];
                }
                catch
                {
                }
            }

            // Gambar lainnya tetap dimuat di background.
            try
            {
                await Task.WhenAll(loadTasks);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Sebagian halaman gagal dimuat: {ex}");
            }

            // =================================================
            // RESTORE POSISI SECARA STABIL
            // =================================================

            if (saved is not null &&
                saved.ScrollMaximum > 0)
            {
                // Tinggi total reader dapat berubah ketika
                // gambar-gambar selesai dimuat. Terapkan posisi
                // beberapa kali agar posisi akhir tetap akurat.
                for (var restoreAttempt = 0;
                     restoreAttempt < 5;
                     restoreAttempt++)
                {
                    await Task.Delay(
                        restoreAttempt == 0 ? 100 : 250);

                    var maximum =
                        ReaderScrollViewer.Extent.Height -
                        ReaderScrollViewer.Viewport.Height;

                    if (maximum <= 0)
                        continue;

                    var targetOffset =
                        Math.Clamp(
                            savedRatio * maximum,
                            0,
                            maximum);

                    ReaderScrollViewer.Offset =
                        new Vector(
                            ReaderScrollViewer.Offset.X,
                            targetOffset);

                    await Avalonia.Threading.Dispatcher.UIThread
                        .InvokeAsync(
                            () => { },
                            Avalonia.Threading.DispatcherPriority.Render);
                }

                // Posisi final.
                var finalMaximum =
                    ReaderScrollViewer.Extent.Height -
                    ReaderScrollViewer.Viewport.Height;

                if (finalMaximum > 0)
                {
                    var finalOffset =
                        Math.Clamp(
                            savedRatio * finalMaximum,
                            0,
                            finalMaximum);

                    ReaderScrollViewer.Offset =
                        new Vector(
                            ReaderScrollViewer.Offset.X,
                            finalOffset);
                }

                ReaderProgress.Text =
                    $"Melanjutkan • {savedRatio * 100.0:0}%";
            }

            if (_readerCurrentPage <= 0)
                _readerCurrentPage = 1;

            // Jangan hitung ulang dari posisi sementara ketika
            // progress lama sedang dipulihkan.
            if (saved is null)
            {
                UpdateReaderProgress();
            }
            else
            {
                ReaderProgress.Text =
                    $"Melanjutkan • {savedRatio * 100.0:0}%";
            }
        }
        catch (Exception ex)
        {
            ReaderProgress.Text =
                $"Gagal: {ex.Message}";
        }
        finally
        {
            _isRestoringReadingProgress = false;
        }
    }

    private async void PreviousChapterButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentChapter is null)
            return;

        var index = _readerChapters.ToList().FindIndex(x => x.Url == _currentChapter.Url);
        if (index > 0)
            await OpenChapterAsync(_readerChapters[index - 1]);
    }

    private async void NextChapterButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentChapter is null)
            return;

        var index = _readerChapters.ToList().FindIndex(x => x.Url == _currentChapter.Url);
        if (index >= 0 && index < _readerChapters.Count - 1)
            await OpenChapterAsync(_readerChapters[index + 1]);
    }

    private void ReaderModeComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var horizontal = string.Equals(
            ReaderModeComboBox.SelectedItem?.ToString(),
            "Horizontal",
            StringComparison.OrdinalIgnoreCase);

        ReaderPageContainer.Orientation = horizontal
            ? Avalonia.Layout.Orientation.Horizontal
            : Avalonia.Layout.Orientation.Vertical;
        ReaderScrollViewer.HorizontalScrollBarVisibility = horizontal
            ? Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
            : Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden;
        ReaderScrollViewer.VerticalScrollBarVisibility = horizontal
            ? Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden
            : Avalonia.Controls.Primitives.ScrollBarVisibility.Auto;
    }

    private async void DownloadChapterButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_currentManga is null || _readerChapters.Count == 0)
            return;

        var chapter = _currentChapter ?? _readerChapters.FirstOrDefault();
        if (chapter is null)
            return;

        try
        {
            DownloadChapterButton.IsEnabled = false;
            DownloadChapterButton.Content = "Mengambil halaman...";
            var pages = await _activeSource.GetPagesAsync(chapter);
            var progress = new Progress<int>(count =>
                DownloadChapterButton.Content = $"Download {count}/{pages.Count}");
            var folder = await _downloadService.DownloadAsync(_currentManga, chapter, pages, progress);
            DownloadChapterButton.Content = $"Tersimpan: {Path.GetFileName(folder)}";
        }
        catch (Exception ex)
        {
            DownloadChapterButton.Content = $"Download gagal: {ex.Message}";
        }
        finally
        {
            DownloadChapterButton.IsEnabled = true;
        }
    }

    private async Task LoadPageImageAsync(Image image, ComicPage page)
    {
        var localPath = await _pageCacheService.GetLocalPathAsync(page);
        if (localPath is not null)
        {
            await using var localStream = File.OpenRead(localPath);
            image.Source = new Bitmap(localStream);
            return;
        }

        using var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/140.0 Safari/537.36");

        if (page.Headers is not null)
        {
            foreach (var header in page.Headers)
                client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
        }

        var bytes = await client.GetByteArrayAsync(page.ImageUrl);

        await using var stream =
            new MemoryStream(bytes);

        var bitmap =
            new Bitmap(stream);

        image.Source = bitmap;
    }


    private async Task LoadCoverAsync(
        Image image,
        string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;

        try
        {
            using var client = new HttpClient();

            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/140.0 Safari/537.36");

            var localPath =
                await _coverCacheService.GetLocalPathAsync(url);

            if (localPath is not null && File.Exists(localPath))
            {
                await using var localStream = File.OpenRead(localPath);
                image.Source = new Bitmap(localStream);
                return;
            }

            var bytes = await client.GetByteArrayAsync(url);

            await using var stream =
                new MemoryStream(bytes);

            image.Source =
                new Bitmap(stream);
        }
        catch
        {
            // Cover gagal tidak boleh membuat UI crash.
        }
    }


    private void DetailBackButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        MangaDetailPage.IsVisible = false;
        SourcePage.IsVisible = true;
        ReaderPage.IsVisible = false;
    }
    private void ReaderScrollViewer_ScrollChanged(
        object? sender,
        Avalonia.Controls.ScrollChangedEventArgs e)
    {
        if (_isRestoringReadingProgress)
            return;

        UpdateReaderProgress();
        ScheduleProgressSave();
    }


    private void ScheduleProgressSave()
    {
        _progressSaveTimer?.Cancel();

        var cts = new CancellationTokenSource();
        _progressSaveTimer = cts;

        _ = SaveProgressAfterDelayAsync(cts);
    }

    private async Task SaveProgressAfterDelayAsync(
        CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(500, cts.Token);

            if (cts.IsCancellationRequested)
                return;

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(
                SaveCurrentReadingProgress);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Gagal menyimpan progress tertunda: {ex}");
        }
    }


    private void UpdateReaderProgress()
    {
        if (_currentChapter is null)
            return;

        var maximum =
            ReaderScrollViewer.Extent.Height -
            ReaderScrollViewer.Viewport.Height;

        if (maximum < 0)
            maximum = 0;

        var offset =
            ReaderScrollViewer.Offset.Y;

        var percentage =
            maximum <= 0
                ? 0
                : Math.Clamp(
                    offset / maximum * 100.0,
                    0,
                    100);

        var pageCount =
            Math.Max(_readerPageCount, 1);

        var page = 1;

        if (maximum > 0 && pageCount > 1)
        {
            page =
                Math.Clamp(
                    (int)Math.Round(
                        offset / maximum *
                        (pageCount - 1)) + 1,
                    1,
                    pageCount);
        }

        _readerCurrentPage = page;

        var chapterNumber =
            _currentChapter.Number > 0
                ? _currentChapter.Number
                : 1;

        var totalChapters =
            Math.Max(
                _readerChapters.Count,
                chapterNumber);

        ReaderProgress.Text =
            $"Chapter {chapterNumber}/{totalChapters} • " +
            $"Page {page}/{pageCount} • " +
            $"{percentage:0}%";
    }


    private void SaveCurrentReadingProgress()
    {
        if (_currentManga is null || _currentChapter is null)
            return;

        var maximum =
            ReaderScrollViewer.Extent.Height -
            ReaderScrollViewer.Viewport.Height;

        if (maximum <= 0)
            return;

        var offset =
            Math.Clamp(
                ReaderScrollViewer.Offset.Y,
                0,
                maximum);

        try
        {
            _readingProgressService.Save(
                _currentManga.Url,
                _currentChapter.Url,
                _currentManga.Title,
                _currentChapter.Name,
                offset,
                maximum,
                _currentManga.CoverUrl);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Gagal menyimpan progress: {ex}");
        }
    }

    private async Task RestoreReadingProgressAsync()
    {
        if (_currentManga is null || _currentChapter is null)
            return;

        ReadingProgress? saved;

        try
        {
            saved = _readingProgressService.Get(
                _currentManga.Url,
                _currentChapter.Url);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Gagal membaca progress: {ex}");
            return;
        }

        if (saved is null || saved.ScrollMaximum <= 0)
            return;

        var ratio =
            Math.Clamp(
                saved.ScrollOffset / saved.ScrollMaximum,
                0,
                1);

        _isRestoringReadingProgress = true;

        try
        {
            // Tunggu layout reader benar-benar terbentuk.
            for (var i = 0; i < 50; i++)
            {
                await Task.Delay(100);

                var maximum =
                    ReaderScrollViewer.Extent.Height -
                    ReaderScrollViewer.Viewport.Height;

                if (maximum <= 0)
                    continue;

                var targetOffset =
                    Math.Clamp(
                        ratio * maximum,
                        0,
                        maximum);

                ReaderScrollViewer.Offset =
                    new Vector(
                        ReaderScrollViewer.Offset.X,
                        targetOffset);

                // Beri kesempatan Avalonia menerapkan Offset.
                await Task.Delay(100);

                ReaderScrollViewer.Offset =
                    new Vector(
                        ReaderScrollViewer.Offset.X,
                        targetOffset);

                UpdateReaderProgress();

                return;
            }
        }
        finally
        {
            _isRestoringReadingProgress = false;
        }
    }
    private async void ReaderBackButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        _progressSaveTimer?.Cancel();
        _progressSaveTimer = null;

        // Simpan langsung, jangan menunggu timer.
        SaveCurrentReadingProgress();

        // Langsung refresh tombol "Lanjut Baca".
        await UpdateResumeReadingButtonAsync();

        ReaderPage.IsVisible = false;
        MangaDetailPage.IsVisible = !_returnToHomeAfterReader;

        MainScrollViewer.IsVisible = true;
        MainScrollViewer.ScrollToHome();

        if (_returnToHomeAfterReader)
        {
            _returnToHomeAfterReader = false;
            (DataContext as BrowseViewModel)?.ReturnToHome();
        }
    }


    private void ReaderTopButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ReaderScrollViewer.ScrollToHome();
    }


    private void ReaderBottomButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        ReaderScrollViewer.ScrollToEnd();
    }


    private async void Refresh_Click(
        object? sender,
        RoutedEventArgs e)
    {
        await LoadExtensionsAsync();
    }
}







































