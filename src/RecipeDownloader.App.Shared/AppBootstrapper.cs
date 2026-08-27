using System.Text.Json;
using RecipeDownloader.App.Platform;
using RecipeDownloader.App.ViewModels;
using RecipeDownloader.Core.Models;
using RecipeDownloader.Core.Providers;
using RecipeDownloader.Core.Providers.BlueApron;
using RecipeDownloader.Core.Providers.Factor;
using RecipeDownloader.Core.Providers.HelloFresh;
using RecipeDownloader.Core.Providers.HomeChef;
using RecipeDownloader.Core.Storage;

namespace RecipeDownloader.App;

/// <summary>
/// Builds the application's object graph. Both desktop hosts share this so the provider
/// list, storage layout, and startup sequence stay identical across platforms.
/// </summary>
public static class AppBootstrapper
{
    private const string UserAgent =
        "RecipeDownloader/1.0 (+https://github.com/jstephenperry/recipe-downloader)";

    private static readonly JsonSerializerOptions RecipeDataJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>
    /// Creates the HTTP client used by every provider.
    /// </summary>
    public static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler
        {
            // Several providers serve compressed responses whether or not they are asked to.
            AutomaticDecompression = System.Net.DecompressionMethods.All
        };

        var httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };

        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        return httpClient;
    }

    /// <summary>
    /// Creates the main view model with every provider registered.
    /// </summary>
    public static MainViewModel CreateMainViewModel(HttpClient httpClient, PlatformServices platform)
    {
        var catalogStore = new RecipeCatalogStore(AppPaths.DataDirectory);
        var pantryStore = new PantryStore(AppPaths.DataDirectory);

        var mainVm = new MainViewModel(platform);
        mainVm.SetPantryStore(pantryStore);

        foreach (var provider in CreateProviders(httpClient))
        {
            mainVm.AddProvider(new ProviderViewModel(
                provider, catalogStore, mainVm.GetOutputDirectory, platform.FileLauncher));
        }

        return mainVm;
    }

    private static IEnumerable<IRecipeProvider> CreateProviders(HttpClient httpClient)
    {
        // HelloFresh publishes one sitemap page per initial letter.
        var letters = Enumerable.Range('a', 26).Select(c => ((char)c).ToString()).ToArray();

        yield return new HelloFreshProvider(httpClient, letters);
        yield return new BlueApronProvider(httpClient);
        yield return new FactorProvider(httpClient);
        yield return new HomeChefProvider(httpClient);
    }

    /// <summary>
    /// Restores cached catalogs, the pantry, and previously downloaded recipe data.
    /// </summary>
    public static async Task LoadPersistedStateAsync(MainViewModel mainVm)
    {
        foreach (var provider in mainVm.Providers)
            await provider.LoadCatalogAsync();

        if (mainVm.PantryViewModel is not null)
            await mainVm.PantryViewModel.LoadAsync();

        mainVm.SetRecipeData(await LoadRecipeDataAsync(mainVm.GetOutputDirectory()));
    }

    /// <summary>
    /// Reads the JSON files written by past downloads so meal planning has data to work with.
    /// </summary>
    private static async Task<List<RecipeData>> LoadRecipeDataAsync(string outputDir)
    {
        var recipes = new List<RecipeData>();

        if (!Directory.Exists(outputDir))
            return recipes;

        foreach (var jsonFile in Directory.EnumerateFiles(outputDir, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                var fileName = Path.GetFileName(jsonFile);
                if (fileName is "settings.json" or "pantry.json" ||
                    fileName.EndsWith("Catalog.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                await using var stream = File.OpenRead(jsonFile);
                var data = await JsonSerializer.DeserializeAsync<RecipeData>(stream, RecipeDataJsonOptions);
                if (data is not null && !string.IsNullOrEmpty(data.Title) && data.Ingredients.Count > 0)
                    recipes.Add(data);
            }
            catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
            {
                // Not a RecipeData document, or unreadable — skip it.
            }
        }

        return recipes;
    }
}
