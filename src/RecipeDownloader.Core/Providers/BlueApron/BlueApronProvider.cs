using System.Text.Json;
using RecipeDownloader.Core.Export;
using RecipeDownloader.Core.Models;
using RecipeDownloader.Core.Providers.Shared;

namespace RecipeDownloader.Core.Providers.BlueApron;

public class BlueApronProvider : IRecipeProvider
{
    private const string SitemapUrl = "https://www.blueapron.com/recipes/sitemap.xml";
    private const int MaxConcurrency = 4;
    private static readonly TimeSpan ThrottleDelay = TimeSpan.FromMilliseconds(300);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;

    public string Name => "Blue Apron";
    public string DownloadFileExtension => ".html";

    public BlueApronProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<Recipe>> DiscoverRecipesAsync(
        IProgress<DiscoveryProgress>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report(new DiscoveryProgress("Fetching sitemap", 0, 0, "recipes/sitemap.xml"));

        List<Recipe> stubs;
        try
        {
            await using var stream = await _httpClient.GetStreamAsync(SitemapUrl, ct);
            stubs = BlueApronScraper.ParseRecipeUrlsFromSitemap(stream);
        }
        catch (HttpRequestException ex)
        {
            progress?.Report(new DiscoveryProgress("Error", 0, 0, $"Failed to fetch sitemap: {ex.Message}"));
            return [];
        }

        progress?.Report(new DiscoveryProgress("Validating recipes", 0, stubs.Count,
            $"Found {stubs.Count} in sitemap — checking for ingredient data..."));

        // Visit each recipe page to check for ingredient data, same pattern as HelloFresh PDF resolution.
        // Recipes without structured ingredients are excluded.
        var validated = await ValidateRecipesAsync(stubs, progress, ct);

        progress?.Report(new DiscoveryProgress("Discovery complete", validated.Count, validated.Count,
            $"{validated.Count} recipes with ingredients (excluded {stubs.Count - validated.Count})"));

        return validated;
    }

    public async Task<string> DownloadRecipeAsync(
        Recipe recipe,
        string outputDirectory,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDirectory);

        var baseName = FileNameSanitizer.Sanitize(recipe.Name);

        var html = await _httpClient.GetStringAsync(recipe.SourceUrl, ct);
        var recipeData = BlueApronScraper.ParseRecipeDataFromPage(html, recipe.SourceUrl);

        if (recipeData is null)
            throw new InvalidOperationException($"Could not extract recipe data from '{recipe.Name}'.");

        // Save JSON
        var jsonPath = Path.Combine(outputDirectory, baseName + ".json");
        var json = JsonSerializer.Serialize(recipeData, JsonOptions);
        await File.WriteAllTextAsync(jsonPath, json, ct);

        // Save HTML recipe card
        var htmlContent = RecipeHtmlGenerator.Generate(recipeData);
        var htmlPath = Path.Combine(outputDirectory, baseName + ".html");
        await File.WriteAllTextAsync(htmlPath, htmlContent, ct);

        return htmlPath;
    }

    private async Task<List<Recipe>> ValidateRecipesAsync(
        List<Recipe> stubs,
        IProgress<DiscoveryProgress>? progress,
        CancellationToken ct)
    {
        var semaphore = new SemaphoreSlim(MaxConcurrency);
        var validated = new List<Recipe>();
        var lockObj = new object();
        var completed = 0;
        var excluded = 0;

        var tasks = stubs.Select(async stub =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();
                await Task.Delay(ThrottleDelay, ct);

                string html;
                try
                {
                    html = await _httpClient.GetStringAsync(stub.SourceUrl, ct);
                }
                catch (HttpRequestException)
                {
                    Interlocked.Increment(ref completed);
                    Interlocked.Increment(ref excluded);
                    return;
                }

                var hasIngredients = BlueApronScraper.PageHasIngredients(html);
                var current = Interlocked.Increment(ref completed);

                if (hasIngredients)
                {
                    var recipe = stub with { PdfUrl = stub.SourceUrl, DiscoveredAt = DateTimeOffset.Now };
                    lock (lockObj)
                    {
                        validated.Add(recipe);
                    }
                }
                else
                {
                    Interlocked.Increment(ref excluded);
                }

                progress?.Report(new DiscoveryProgress(
                    "Validating recipes", current, stubs.Count,
                    $"{validated.Count} valid, {excluded} excluded"));
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return validated;
    }

}
