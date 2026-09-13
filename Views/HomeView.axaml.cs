using Avalonia.Controls;
using Avalonia.Interactivity;
using ComicReader.ViewModels;

namespace ComicReader.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();
    }

    private void ResumeButton_Click(
        object? sender,
        RoutedEventArgs e)
    {
        if (sender is not Button button)
            return;

        if (button.DataContext is not HomeReadingItem item)
            return;

        if (DataContext is not HomeViewModel viewModel)
            return;

        viewModel.Resume(item);
    }
}
