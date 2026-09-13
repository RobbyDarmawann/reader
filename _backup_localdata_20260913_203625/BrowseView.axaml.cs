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

namespace ComicReader.Views;

public partial class BrowseView : UserControl
{
    private readonly BrowseViewModel _viewModel;
    private readonly SourceSearchViewModel _searchViewModel;
    private readonly BacaKomikCatalogSource _bacaCatalog;
    private readonly BacaKomikSource _bacaSource;
    private readonly ReadingProgressService _readingProgress;

    private Manga? _currentManga;
    private Chapter? _currentChapter;

    private readonly ReadingProgressService _readingProgressService =
        new ReadingProgressService();

    private int _readerPageCount;
    private int _readerCurrentPage;
    private IReadOnlyList<Chapter> _readerChapters =
        Array.Empty<Chapter>();

    private CancellationTokenSource? _progressSaveTimer;
    private bool _isRestoringReadingProgress;
    public BrowseView()
    {
        InitializeComponent();

        _viewModel = new BrowseViewModel();
        _searchViewModel = new SourceSearchViewModel();
        _bacaCatalog = new BacaKomikCatalogSource();
        _bacaSource = new BacaKomikSource();
        _readingProgress = new ReadingProgressService();

        BuildSections();
        BuildFilters();

        Loaded += BrowseView_Loaded;

        RefreshButton.Click += Refresh_Click;
        BackButton.Click += BackToExtensions_Click;
        SearchButton.Click += Search_Click;
        ApplyFilterButton.Click += ApplyFilter_Click;
        DetailBackButton.Click += DetailBackButton_Click;

        ReaderBackButton.Click += ReaderBackButton_Click;
        ResumeReadingButton.Click += ResumeReadingButton_Click;
        ReaderTopButton.Click += ReaderTopButton_Click;
        ReaderBottomButton.Click += ReaderBottomButton_Click;
        ReaderScrollViewer.ScrollChanged += ReaderScrollViewer_ScrollChanged;
    }


    private async void BrowseView_Loaded(
        object? sender,
        RoutedEventArgs e)
    {
        await LoadExtensionsAsync();
    }


    private async Task LoadExtensionsAsync()
    {
        try
        {
            await _viewModel.LoadExtensionsAsync();

            BuildInstalledSources();
            BuildExtensions();
        }
        catch (Exception ex)
        {
            InstalledStatus.Text = $"Gagal memuat: {ex.Message}";
            ExtensionStatus.Text = ex.Message;
        }
    }


    private void BuildInstalledSources()
    {
        InstalledContainer.Children.Clear();

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

                InstalledContainer.Children.Add(
                    CreateSourceCard(
                        source.Id.ToString(),
                        source.Name,
                        $"{source.Language} • {source.BaseUrl}"));
            }
        }

        InstalledStatus.Text =
            InstalledContainer.Children.Count == 0
                ? "Belum ada source."
                : $"{InstalledContainer.Children.Count} source tersedia.";
    }


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
            Background = Avalonia.Media.Brushes.Transparent
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
                Padding = new Thickness(12),
                Margin = new Thickness(0, 0, 0, 8)
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
                    FontSize = 16,
                    FontWeight = Avalonia.Media.FontWeight.Bold
                });

            text.Children.Add(
                new TextBlock
                {
                    Text =
                        $"{extension.VersionName} • {extension.Sources.Count} source",
                    FontSize = 13,
                    Opacity = 0.6
                });

            grid.Children.Add(text);

            var install = new Button
            {
                Content =
                    _viewModel.InstalledPackages.Contains(
                        extension.PackageName)
                        ? "Installed"
                        : "Install",
                Width = 100
            };

            Grid.SetColumn(install, 1);
            grid.Children.Add(install);

            install.Click += async (_, _) =>
            {
                try
                {
                    install.IsEnabled = false;

                    await _viewModel.InstallAsync(extension);

                    BuildInstalledSources();

                    install.Content = "Installed";
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
                "Source terdeteksi dari extension, tetapi adapter native belum tersedia.";

            SearchBox.IsEnabled = false;
            SearchButton.IsEnabled = false;
            FilterBorder.IsVisible = false;
            MangaGrid.Items.Clear();

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


    private async Task LoadCatalogAsync()
    {
        try
        {
            CatalogStatus.Text = "Memuat katalog...";

            var request = new SourceCatalogRequest(
                Section: _currentSection,
                Genre: GetComboValue(GenreComboBox),
                Type: GetComboValue(TypeComboBox),
                Status: GetComboValue(StatusComboBox),
                Format: GetComboValue(FormatComboBox),
                Sort: GetComboValue(SortComboBox));

            var results =
                await _bacaCatalog.GetCatalogAsync(request);

            RenderMangaGrid(results);

            CatalogStatus.Text =
                $"{results.Count} manga ditemukan.";
        }
        catch (Exception ex)
        {
            CatalogStatus.Text =
                $"Gagal memuat katalog: {ex.Message}";
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

            var results =
                await _bacaSource.SearchAsync(query);

            RenderMangaGrid(results);

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


    private Control CreateMangaCard(
        Manga manga)
    {
        var border = new Border
        {
            Background = Avalonia.Media.Brushes.Transparent,
            Margin = new Thickness(6),
            Padding = new Thickness(8),
            Width = 165,
            Height = 300
        };

        var stack = new StackPanel();

        var image = new Image
        {
            Width = 145,
            Height = 205,
            Stretch = Avalonia.Media.Stretch.UniformToFill
        };

        stack.Children.Add(image);

        _ = LoadCoverAsync(image, manga.CoverUrl);

        stack.Children.Add(
            new TextBlock
            {
                Text = manga.Title,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxHeight = 48,
                Margin = new Thickness(0, 8, 0, 6),
                FontWeight = Avalonia.Media.FontWeight.Bold
            });

        var open = new Button
        {
            Content = "Open",
            HorizontalAlignment =
                Avalonia.Layout.HorizontalAlignment.Stretch
        };

        open.Click += async (_, _) =>
            await OpenMangaDetailAsync(manga);

        stack.Children.Add(open);

        border.Child = stack;

        return border;
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
        DetailSource.Text = "BacaKomik";

        DetailAuthor.Text =
            $"Author: {manga.Author ?? "Tidak diketahui"}";

        DetailArtist.Text =
            $"Artist: {manga.Artist ?? "Tidak diketahui"}";

        DetailStatus.Text = "";
        DetailType.Text = "";
        DetailGenres.Text = "";

        DetailDescription.Text =
            "Memuat synopsis...";

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
                await _bacaSource.GetDetailsAsync(manga);

            if (details is not null)
            {
                DetailTitle.Text = details.Title;

                DetailAuthor.Text =
                    $"Author: {details.Author ?? "Tidak diketahui"}";

                DetailArtist.Text =
                    $"Artist: {details.Artist ?? "Tidak diketahui"}";

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
                await _bacaSource.GetChaptersAsync(manga);

            _readerChapters = chapters;

            RenderChapters(chapters);

            ChapterStatus.Text =
                chapters.Count == 0
                    ? "Tidak ada chapter."
                    : $"{chapters.Count} chapter tersedia.";

            await UpdateResumeReadingButtonAsync();
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

        ReaderPage.IsVisible = true;
        ExtensionPage.IsVisible = false;
        SourcePage.IsVisible = false;
        MangaDetailPage.IsVisible = false;

        MainScrollViewer.IsVisible = false;

        ReaderTitle.Text =
            _currentManga?.Title ?? "Reader";

        ReaderChapter.Text =
            chapter.Name;

        ReaderProgress.Text =
            "Memuat...";

        ReaderPageContainer.Children.Clear();

        _isRestoringReadingProgress = true;

        try
        {
            var pages =
                await _bacaSource.GetPagesAsync(chapter);

            _readerPageCount = pages.Count;
            _readerCurrentPage = pages.Count > 0 ? 1 : 0;

            if (pages.Count == 0)
            {
                ReaderProgress.Text =
                    "Tidak ada halaman.";

                return;
            }

            // Ambil progress chapter ini sebelum gambar dimuat.
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

            // Setelah seluruh layout stabil, rapikan posisi ke
            // persentase progress yang sebenarnya.
            if (saved is not null &&
                saved.ScrollMaximum > 0)
            {
                await Task.Delay(100);

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
            }

            if (_readerCurrentPage <= 0)
                _readerCurrentPage = 1;

            UpdateReaderProgress();
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

    private async Task LoadPageImageAsync(Image image, ComicPage page)
    {
        using var client = new HttpClient();

        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
            "AppleWebKit/537.36 (KHTML, like Gecko) " +
            "Chrome/140.0 Safari/537.36");

        if (page.Headers is not null)
        {
            foreach (var header in page.Headers)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    header.Key,
                    header.Value);
            }
        }

        var bytes =
            await client.GetByteArrayAsync(page.ImageUrl);

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

            var bytes =
                await client.GetByteArrayAsync(url);

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
        UpdateReaderProgress();

        if (!_isRestoringReadingProgress)
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
                maximum);
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
        MangaDetailPage.IsVisible = true;

        MainScrollViewer.IsVisible = true;
        MainScrollViewer.ScrollToHome();
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























