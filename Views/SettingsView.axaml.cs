using Avalonia.Controls;
using Avalonia.Interactivity;
using ComicReader.ViewModels;

namespace ComicReader.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void SaveDownloadFolder_Click(object? sender, RoutedEventArgs e)
    {
        (DataContext as SettingsViewModel)?.SaveDownloadFolder();
    }
}