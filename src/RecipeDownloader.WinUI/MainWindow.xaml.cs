using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using RecipeDownloader.ViewModels;

namespace RecipeDownloader.WinUI;

public sealed partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        Title = "Recipe Downloader";
    }

    public void SetViewModel(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        Root.DataContext = viewModel;
    }

    private void Nav_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer?.Tag is string viewName && _viewModel is not null)
            _viewModel.NavigateToCommand.Execute(viewName);
    }
}
