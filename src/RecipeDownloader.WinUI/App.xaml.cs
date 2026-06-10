using System.Net.Http;
using Microsoft.UI.Xaml;
using RecipeDownloader.Core.Providers.BlueApron;
using RecipeDownloader.Core.Providers.HelloFresh;
using RecipeDownloader.Core.Storage;
using RecipeDownloader.ViewModels;
using RecipeDownloader.WinUI.Services;
using WinRT.Interop;

namespace RecipeDownloader.WinUI;

public partial class App : Application
{
    private HttpClient? _httpClient;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += static (_, e) => e.SetObserved();
    }

    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "RecipeDownloader/1.0 (+https://github.com/recipe-downloader)");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RecipeDownloader");

        var catalogStore = new RecipeCatalogStore(dataDir);
        var pantryStore = new PantryStore(dataDir);

        _window = new MainWindow();
        var hwnd = WindowNative.GetWindowHandle(_window);

        var mainVm = new MainViewModel(
            new WinUIFolderPickerService(() => hwnd),
            new WinUIClipboardService());
        mainVm.SetPantryStore(pantryStore);

        // Register providers — all letters A-Z
        var allLetters = Enumerable.Range('a', 26).Select(c => ((char)c).ToString()).ToArray();
        var helloFresh = new HelloFreshProvider(_httpClient, allLetters);
        mainVm.AddProvider(new ProviderViewModel(helloFresh, catalogStore, mainVm.GetOutputDirectory));

        // Blue Apron — all recipes from XML sitemap
        var blueApron = new BlueApronProvider(_httpClient);
        mainVm.AddProvider(new ProviderViewModel(blueApron, catalogStore, mainVm.GetOutputDirectory));

        _window.SetViewModel(mainVm);
        _window.Activate();

        // Load cached catalogs — guarded so a corrupt file doesn't prevent startup
        foreach (var provider in mainVm.Providers)
        {
            try
            {
                await provider.LoadCatalogAsync();
            }
            catch
            {
                // Corrupt cache is non-fatal; user can re-discover
            }
        }

        try
        {
            await mainVm.PantryViewModel!.LoadAsync();
        }
        catch
        {
            // Corrupt pantry file is non-fatal; starts empty
        }

        // Load recipe data from downloaded JSON files for meal planning
        var recipeData = await RecipeDataLoader.LoadAllAsync(mainVm.GetOutputDirectory());
        mainVm.SetRecipeData(recipeData);
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        // Keep the app alive; the error surfaces via status text where applicable
        e.Handled = true;
    }
}
