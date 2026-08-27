using System.Net.Http;
using System.Windows;
using RecipeDownloader.App.Platform;
using RecipeDownloader.App.Views;
using RecipeDownloader.App.Wpf.Platform;

namespace RecipeDownloader.App;

public partial class App : Application
{
    private HttpClient? _httpClient;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _httpClient = AppBootstrapper.CreateHttpClient();

        var platform = new PlatformServices(
            new WpfFolderPicker(),
            new WpfClipboardService(),
            new ProcessFileLauncher());

        var mainVm = AppBootstrapper.CreateMainViewModel(_httpClient, platform);

        var mainWindow = new MainWindow { DataContext = mainVm };
        mainWindow.Show();

        await AppBootstrapper.LoadPersistedStateAsync(mainVm);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _httpClient?.Dispose();
        base.OnExit(e);
    }
}
