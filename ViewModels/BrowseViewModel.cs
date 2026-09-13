using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ComicReader.Infrastructure.Extensions;
using ComicReader.Infrastructure.Reading;

namespace ComicReader.ViewModels;

public sealed class BrowseViewModel : INotifyPropertyChanged
{
    public event Action? ReturnToHomeRequested;

    public void ReturnToHome() => ReturnToHomeRequested?.Invoke();

    private readonly KeiyoushiRepositoryService _repositoryService = new();
    private readonly ExtensionInstallService _installService = new();

    private string _statusText = "Belum memuat extension.";
    private string _selectedLanguage = "All languages";
    private string _extensionQuery = string.Empty;

    private const string ResumeMarker = "local-resume";

    public ReadingProgress? PendingResume { get; private set; }
    public ReadingProgress? PendingDetail { get; private set; }

    public void RequestResume(
        ReadingProgress progress)
    {
        PendingResume = progress;
    }

    public void RequestDetail(ReadingProgress progress)
    {
        PendingDetail = progress;
    }

    public ReadingProgress? TakePendingDetail()
    {
        var progress = PendingDetail;
        PendingDetail = null;
        return progress;
    }

    public ReadingProgress? TakePendingResume()
    {
        var progress = PendingResume;
        PendingResume = null;
        return progress;
    }
    public ObservableCollection<KeiyoushiExtension> Extensions { get; } = new();

    public ObservableCollection<KeiyoushiExtension> FilteredExtensions { get; } = new();

    public ObservableCollection<KeiyoushiExtension> InstalledSources { get; } = new();

    public ObservableCollection<string> Languages { get; } = new()
    {
        "All languages"
    };

    public HashSet<string> InstalledPackages { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public string StatusText
    {
        get => _statusText;
        private set
        {
            if (_statusText == value)
                return;

            _statusText = value;
            OnPropertyChanged();
        }
    }

    public string SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (_selectedLanguage == value)
                return;

            _selectedLanguage = value;
            OnPropertyChanged();

            ApplyFilter();
        }
    }

    public string ExtensionQuery
    {
        get => _extensionQuery;
        set
        {
            if (_extensionQuery == value)
                return;

            _extensionQuery = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    private void ApplyRepository(
        KeiyoushiRepository repository)
    {
        Extensions.Clear();
        FilteredExtensions.Clear();
        Languages.Clear();

        Languages.Add("All languages");

        foreach (var extension in repository.Extensions
                     .OrderBy(
                         x => x.Name,
                         StringComparer.OrdinalIgnoreCase))
        {
            Extensions.Add(extension);

            foreach (var language in extension.Sources
                         .Select(x => x.Language)
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Languages.Contains(language))
                {
                    Languages.Add(language);
                }
            }
        }

        LoadInstalledPackages();
        LoadInstalledSources();
        ApplyFilter();
    }

    private async Task RefreshExtensionsInBackgroundAsync()
    {
        try
        {
            var repository =
                await _repositoryService.LoadAsync();

            ApplyRepository(repository);

            StatusText =
                $"{Extensions.Count} extension tersedia dari {repository.Name}.";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Background extension refresh gagal: {ex}");
        }
    }

    public async Task LoadExtensionsAsync()
    {
        // ====================================================
        // LOCAL FIRST
        // ====================================================

        try
        {
            var cached =
                await _repositoryService.LoadCachedAsync();

            if (cached is not null &&
                cached.Extensions.Count > 0)
            {
                ApplyRepository(cached);

                StatusText =
                    $"{Extensions.Count} extension tersedia (lokal).";

                // Jangan menunggu internet.
                // Refresh berjalan setelah UI sudah mendapatkan
                // data lokal.
                _ = RefreshExtensionsInBackgroundAsync();

                return;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Gagal membaca cache Keiyoushi: {ex}");
        }

        // ====================================================
        // BELUM ADA CACHE -> DOWNLOAD SEKARANG
        // ====================================================

        StatusText =
            "Mengambil daftar extension dari Keiyoushi...";

        try
        {
            var repository =
                await _repositoryService.LoadAsync();

            ApplyRepository(repository);

            StatusText =
                $"{Extensions.Count} extension tersedia dari {repository.Name}.";
        }
        catch (Exception ex)
        {
            StatusText =
                $"Gagal memuat extension: {ex.Message}";
        }
    }
    public async Task InstallAsync(
        KeiyoushiExtension extension)
    {
        if (string.IsNullOrWhiteSpace(extension.ApkUrl))
        {
            StatusText =
                $"APK untuk {extension.Name} tidak tersedia.";

            return;
        }

        try
        {
            StatusText =
                $"Mengunduh {extension.Name}...";

            await _installService.InstallAsync(extension);

            InstalledPackages.Add(
                extension.PackageName);

            LoadInstalledSources();

            StatusText =
                $"{extension.Name} berhasil diunduh.";
        }
        catch (Exception ex)
        {
            StatusText =
                $"Gagal mengunduh {extension.Name}: {ex.Message}";
        }
    }

    public async Task UninstallAsync(KeiyoushiExtension extension)
    {
        try
        {
            StatusText = $"Menghapus {extension.Name}...";
            await _installService.UninstallAsync(extension.PackageName);
            InstalledPackages.Remove(extension.PackageName);
            LoadInstalledSources();
            StatusText = $"{extension.Name} berhasil dihapus. Silakan install ulang untuk uji coba.";
        }
        catch (Exception ex)
        {
            StatusText = $"Gagal menghapus {extension.Name}: {ex.Message}";
        }
    }

    private void ApplyFilter()
    {
        FilteredExtensions.Clear();

        foreach (var extension in Extensions)
        {
            if (SelectedLanguage == "All languages" ||
                extension.Sources.Any(source =>
                    string.Equals(
                        source.Language,
                        SelectedLanguage,
                        StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrWhiteSpace(ExtensionQuery) ||
                    extension.Name.Contains(ExtensionQuery, StringComparison.OrdinalIgnoreCase) ||
                    extension.Sources.Any(source =>
                        source.Name.Contains(ExtensionQuery, StringComparison.OrdinalIgnoreCase)))
                {
                    FilteredExtensions.Add(extension);
                }
            }
        }
    }

    private void LoadInstalledPackages()
    {
        InstalledPackages.Clear();

        foreach (var packageName in
                 _installService.GetInstalledPackages())
        {
            InstalledPackages.Add(packageName);
        }
    }

    private void LoadInstalledSources()
    {
        InstalledSources.Clear();

        foreach (var extension in Extensions)
        {
            if (InstalledPackages.Contains(
                    extension.PackageName))
            {
                InstalledSources.Add(extension);
            }
        }

        OnPropertyChanged(
            nameof(InstalledSources));
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


