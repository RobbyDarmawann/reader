using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using ComicReader.Services;
using ComicReader.Infrastructure.Storage;
using Avalonia.Styling;

namespace ComicReader;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        var preferences = new UserPreferencesService().Load();
        RequestedThemeVariant =
            string.Equals(preferences.Theme, "Light", StringComparison.OrdinalIgnoreCase)
                ? ThemeVariant.Light
                : ThemeVariant.Dark;

        var databaseService = new DatabaseService();

        await databaseService.InitializeAsync();

        if (ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}