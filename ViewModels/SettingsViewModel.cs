using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Styling;
using ComicReader.Infrastructure.Storage;

namespace ComicReader.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private readonly UserPreferencesService _preferencesService = new();
    private readonly UserPreferences _preferences;
    private string _statusText = "Preferensi tersimpan di perangkat ini.";

    public SettingsViewModel()
    {
        _preferences = _preferencesService.Load();
        ApplyTheme();
    }

    public IReadOnlyList<string> Themes { get; } = new[] { "Dark", "Light" };
    public IReadOnlyList<string> ReadingModes { get; } = new[] { "Vertical", "Horizontal" };

    public string SelectedTheme
    {
        get => _preferences.Theme;
        set
        {
            if (_preferences.Theme == value)
                return;

            _preferences.Theme = value;
            ApplyTheme();
            Save();
            OnPropertyChanged();
        }
    }

    public string SelectedReadingMode
    {
        get => _preferences.ReadingMode;
        set
        {
            if (_preferences.ReadingMode == value)
                return;

            _preferences.ReadingMode = value;
            Save();
            OnPropertyChanged();
        }
    }

    public string DownloadFolder
    {
        get => _preferences.DownloadFolder;
        set
        {
            if (_preferences.DownloadFolder == value)
                return;

            _preferences.DownloadFolder = value;
            Save();
            OnPropertyChanged();
        }
    }

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

    public void SaveDownloadFolder()
    {
        Save();
        StatusText = "Pengaturan download berhasil disimpan.";
    }

    private void Save()
    {
        try
        {
            _preferencesService.Save(_preferences);
        }
        catch (Exception ex)
        {
            StatusText = $"Gagal menyimpan pengaturan: {ex.Message}";
        }
    }

    private void ApplyTheme()
    {
        if (Application.Current is not null)
        {
            Application.Current.RequestedThemeVariant =
                string.Equals(_preferences.Theme, "Light", StringComparison.OrdinalIgnoreCase)
                    ? ThemeVariant.Light
                    : ThemeVariant.Dark;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}