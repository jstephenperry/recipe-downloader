using System.Text.Json;
using RecipeDownloader.Core.Export;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Providers.Shared;

/// <summary>
/// Shared implementation for providers that discover a list of recipe page URLs and then
/// extract structured data from each page. Subclasses supply the discovery source and the
/// page parser; this class handles concurrency limiting, throttling, progress reporting,
/// validation, and writing the JSON + HTML artifacts.
/// </summary>
public abstract class RecipeProviderBase : IRecipeProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected RecipeProviderBase(HttpClient httpClient)
    {
        HttpClient = httpClient;
    }

    protected HttpClient HttpClient { get; }

    public abstract string Name { get; }

    public virtual string DownloadFileExtension => ".html";

    /// <summary>
    /// Maximum simultaneous page fetches. Kept deliberately low by default: meal-kit sites are
    /// small operations and aggressive crawling gets the client rate-limited or blocked.
    /// </summary>
    protected virtual int MaxConcurrency => 4;

    /// <summary>
    /// Delay applied before each page fetch, per concurrency slot.
    /// </summary>
    protected virtual TimeSpan ThrottleDelay => TimeSpan.FromMilliseconds(300);

    /// <summary>
    /// When true, discovery visits every candidate page and keeps only those that yield
    /// ingredients. This costs one request per candidate but means the catalog contains
    /// nothing that will fail at download time.
    /// </summary>
    protected virtual bool ValidateDuringDiscovery => true;

    /// <summary>
    /// Produces the candidate recipe pages for this provider.
    /// </summary>
    protected abstract Task<List<Recipe>> DiscoverCandidatesAsync(
        IProgress<DiscoveryProgress>? progress,
        CancellationToken ct);

    /// <summary>
    /// Extracts structured recipe data from a fetched recipe page.
    /// </summary>
    protected abstract RecipeData? ParseRecipe(string html, string sourceUrl);

    public async Task<IReadOnlyList<Recipe>> DiscoverRecipesAsync(
        IProgress<DiscoveryProgress>? progress = null,
        CancellationToken ct = default)
    {
        var candidates = await DiscoverCandidatesAsync(progress, ct);

        if (!ValidateDuringDiscovery)
        {
            progress?.Report(new DiscoveryProgress(
                "Discovery complete", candidates.Count, candidates.Count,
                $"{candidates.Count} recipes"));
            return candidates;
        }

        progress?.Report(new DiscoveryProgress(
            "Validating recipes", 0, candidates.Count,
            $"Found {candidates.Count} candidates — checking for ingredient data..."));

        var validated = await ValidateAsync(candidates, progress, ct);

        progress?.Report(new DiscoveryProgress(
            "Discovery complete", validated.Count, validated.Count,
            $"{validated.Count} recipes with ingredients (excluded {candidates.Count - validated.Count})"));

        return validated;
    }

    public virtual async Task<string> DownloadRecipeAsync(
        Recipe recipe,
        string outputDirectory,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDirectory);

        var html = await HttpClient.GetStringAsync(recipe.SourceUrl, ct);
        var recipeData = ParseRecipe(html, recipe.SourceUrl)
            ?? throw new InvalidOperationException($"Could not extract recipe data from '{recipe.Name}'.");

        return await WriteRecipeDataAsync(recipeData, recipe.Name, outputDirectory, ct);
    }

    /// <summary>
    /// Writes the recipe as JSON plus a rendered HTML card and returns the HTML path.
    /// </summary>
    protected static async Task<string> WriteRecipeDataAsync(
        RecipeData recipeData,
        string recipeName,
        string outputDirectory,
        CancellationToken ct)
    {
        var baseName = FileNameSanitizer.Sanitize(recipeName);

        var jsonPath = Path.Combine(outputDirectory, baseName + ".json");
        var json = JsonSerializer.Serialize(recipeData, JsonOptions);
        await File.WriteAllTextAsync(jsonPath, json, ct);

        var htmlPath = Path.Combine(outputDirectory, baseName + ".html");
        await File.WriteAllTextAsync(htmlPath, RecipeHtmlGenerator.Generate(recipeData), ct);

        return htmlPath;
    }

    private async Task<List<Recipe>> ValidateAsync(
        List<Recipe> candidates,
        IProgress<DiscoveryProgress>? progress,
        CancellationToken ct)
    {
        using var semaphore = new SemaphoreSlim(MaxConcurrency);
        var validated = new List<Recipe>();
        var lockObj = new object();
        var completed = 0;
        var excluded = 0;

        var tasks = candidates.Select(async candidate =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();

                if (ThrottleDelay > TimeSpan.Zero)
                    await Task.Delay(ThrottleDelay, ct);

                string html;
                try
                {
                    html = await HttpClient.GetStringAsync(candidate.SourceUrl, ct);
                }
                catch (HttpRequestException)
                {
                    Interlocked.Increment(ref completed);
                    Interlocked.Increment(ref excluded);
                    return;
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested)
                {
                    // Request timeout rather than user cancellation.
                    Interlocked.Increment(ref completed);
                    Interlocked.Increment(ref excluded);
                    return;
                }

                var data = ParseRecipe(html, candidate.SourceUrl);
                var current = Interlocked.Increment(ref completed);

                if (data is not null && data.Ingredients.Count > 0)
                {
                    var resolved = candidate with
                    {
                        Name = string.IsNullOrWhiteSpace(data.Title) ? candidate.Name : data.Title,
                        DiscoveredAt = DateTimeOffset.Now
                    };

                    lock (lockObj)
                    {
                        validated.Add(resolved);
                    }
                }
                else
                {
                    Interlocked.Increment(ref excluded);
                }

                progress?.Report(new DiscoveryProgress(
                    "Validating recipes", current, candidates.Count,
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
