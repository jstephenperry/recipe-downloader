using System.Net.Http;
using System.Text.Json;
using System.Windows;
using RecipeDownloader.App.ViewModels;
using RecipeDownloader.App.Views;
using RecipeDownloader.Core.Models;
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
        var pantryStore = new PantryStore(dataDir);

        var mainVm = new MainViewModel();
        mainVm.SetPantryStore(pantryStore);

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

        // Load pantry
        await mainVm.PantryViewModel!.LoadAsync();

        // Load recipe data from downloaded JSON files for meal planning
        var recipeData = await LoadRecipeDataAsync(mainVm.GetOutputDirectory());
        mainVm.SetRecipeData(recipeData);
    }

    private static async Task<List<RecipeData>> LoadRecipeDataAsync(string outputDir)
    {
        var recipes = new List<RecipeData>();

        if (!Directory.Exists(outputDir))
            return recipes;

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        foreach (var jsonFile in Directory.EnumerateFiles(outputDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                // Skip catalog/settings files
                var fileName = Path.GetFileName(jsonFile);
                if (fileName is "settings.json" or "pantry.json" ||
                    fileName.EndsWith("Catalog.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                await using var stream = File.OpenRead(jsonFile);
                var data = await JsonSerializer.DeserializeAsync<RecipeData>(stream, jsonOptions);
                if (data is not null && !string.IsNullOrEmpty(data.Title) && data.Ingredients.Count > 0)
                    recipes.Add(data);
            }
            catch
            {
                // Skip files that aren't valid RecipeData JSON
            }
        }

        return recipes;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _httpClient?.Dispose();
        base.OnExit(e);
    }
}
