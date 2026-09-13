using Avalonia.Controls;
using ComicReader.ViewModels;

namespace ComicReader;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        DataContext = new MainWindowViewModel();
    }
}