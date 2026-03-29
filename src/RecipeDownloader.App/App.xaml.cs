using System.Net.Http;
using System.Windows;
using RecipeDownloader.App.ViewModels;
using RecipeDownloader.App.Views;
using RecipeDownloader.Core.Providers.BlueApron;
using RecipeDownloader.Core.Providers.HelloFresh;
using RecipeDownloader.Core.Storage;

namespace RecipeDownloader.App;

public partial class App : Application
{
    private HttpClient? _httpClient;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "RecipeDownloader/1.0 (+https://github.com/recipe-downloader)");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecipeDownloader");

        var catalogStore = new RecipeCatalogStore(dataDir);
        var mainVm = new MainViewModel();

        // Register providers — all letters A-Z
        var allLetters = Enumerable.Range('a', 26).Select(c => ((char)c).ToString()).ToArray();
        var helloFresh = new HelloFreshProvider(_httpClient, allLetters);
        var helloFreshVm = new ProviderViewModel(helloFresh, catalogStore, mainVm.GetOutputDirectory);
        mainVm.AddProvider(helloFreshVm);

        // Blue Apron — all recipes from XML sitemap
        var blueApron = new BlueApronProvider(_httpClient);
        var blueApronVm = new ProviderViewModel(blueApron, catalogStore, mainVm.GetOutputDirectory);
        mainVm.AddProvider(blueApronVm);

        var mainWindow = new MainWindow { DataContext = mainVm };
        mainWindow.Show();

        // Load cached catalogs
        foreach (var provider in mainVm.Providers)
        {
            await provider.LoadCatalogAsync();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _httpClient?.Dispose();
        base.OnExit(e);
    }
}
