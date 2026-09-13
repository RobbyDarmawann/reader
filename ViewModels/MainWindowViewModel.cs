using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ComicReader.Infrastructure.Reading;

namespace ComicReader.ViewModels;

public class MainWindowViewModel : INotifyPropertyChanged
{
    private object _currentViewModel;

    public object CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            if (_currentViewModel == value)
                return;

            _currentViewModel = value;
            OnPropertyChanged();
        }
    }

    public ICommand NavigateHomeCommand { get; }
    public ICommand NavigateLibraryCommand { get; }
    public ICommand NavigateBrowseCommand { get; }
    public ICommand NavigateDownloadsCommand { get; }
    public ICommand NavigateHistoryCommand { get; }
    public ICommand NavigateSettingsCommand { get; }

    public MainWindowViewModel()
    {
        _currentViewModel =
            CreateHomeViewModel();

        NavigateHomeCommand = new RelayCommand(
            () => CurrentViewModel =
                CreateHomeViewModel());

        NavigateLibraryCommand = new RelayCommand(
            () => CurrentViewModel =
                new LibraryViewModel());

        NavigateBrowseCommand = new RelayCommand(
            () => CurrentViewModel =
                new BrowseViewModel());

        NavigateDownloadsCommand = new RelayCommand(
            () => CurrentViewModel =
                new DownloadsViewModel());

        NavigateHistoryCommand = new RelayCommand(
            () => CurrentViewModel =
                new HistoryViewModel());

        NavigateSettingsCommand = new RelayCommand(
            () => CurrentViewModel =
                new SettingsViewModel());
    }

    private HomeViewModel CreateHomeViewModel()
    {
        var home =
            new HomeViewModel();

        home.ResumeRequested +=
            OpenResumeFromHome;

        return home;
    }

    private void OpenResumeFromHome(
        ReadingProgress progress)
    {
        var browse =
            new BrowseViewModel();

        browse.RequestResume(progress);

        CurrentViewModel = browse;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged(
        [CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
    }
}
