using System.Net.Http;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using RecipeDownloader.App.Avalonia.Platform;
using RecipeDownloader.App.Avalonia.Views;
using RecipeDownloader.App.Platform;

namespace RecipeDownloader.App.Avalonia;

public partial class App : Application
{
    private HttpClient? _httpClient;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _httpClient = AppBootstrapper.CreateHttpClient();

            // The window has to exist before the platform services are built: both the folder
            // picker and the clipboard are reached through the window's top level in Avalonia.
            var mainWindow = new MainWindow();

            var platform = new PlatformServices(
                new AvaloniaFolderPicker(mainWindow),
                new AvaloniaClipboardService(mainWindow),
                new ProcessFileLauncher());

            var mainVm = AppBootstrapper.CreateMainViewModel(_httpClient, platform);
            mainWindow.DataContext = mainVm;
            desktop.MainWindow = mainWindow;

            desktop.Exit += (_, _) => _httpClient?.Dispose();

            mainWindow.Opened += async (_, _) => await AppBootstrapper.LoadPersistedStateAsync(mainVm);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
