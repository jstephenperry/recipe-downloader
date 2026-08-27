using System.Text.Json;
using RecipeDownloader.Core.Export;
using RecipeDownloader.Core.Models;
using RecipeDownloader.Core.Providers.Shared;

namespace RecipeDownloader.Core.Providers.HelloFresh;

public class HelloFreshProvider : IRecipeProvider
{
    private const string SitemapUrlTemplate = "https://www.hellofresh.com/pages/sitemap/recipes-{0}";
    private const int MaxConcurrency = 80;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly string[] _letters;

    public string Name => "HelloFresh";
    public string DownloadFileExtension => ".pdf";

    public HelloFreshProvider(HttpClient httpClient, params string[] letters)
    {
        _httpClient = httpClient;
        _letters = letters.Length > 0 ? letters : ["a"];
    }

    public async Task<IReadOnlyList<Recipe>> DiscoverRecipesAsync(
        IProgress<DiscoveryProgress>? progress = null,
        CancellationToken ct = default)
    {
        progress?.Report(new DiscoveryProgress("Discovering recipes", 0, 0, "Fetching sitemaps..."));

        // Shared state across all letters. A single semaphore caps total in-flight
        // recipe-page fetches so pipelining across letters never exceeds MaxConcurrency.
        var semaphore = new SemaphoreSlim(MaxConcurrency);
        var resolved = new List<Recipe>();
        var lockObj = new object();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var totalStubs = 0;
        var completed = 0;

        async Task ResolveStubAsync(Recipe stub)
        {
            await semaphore.WaitAsync(ct);
            try
            {
                ct.ThrowIfCancellationRequested();

                string html;
                try
                {
                    html = await _httpClient.GetStringAsync(stub.SourceUrl, ct);
                }
                catch (HttpRequestException)
                {
                    Interlocked.Increment(ref completed);
                    return;
                }

                var pdfUrl = HelloFreshScraper.ParsePdfUrlFromRecipePage(html);
                var current = Interlocked.Increment(ref completed);

                if (pdfUrl is not null)
                {
                    var recipe = stub with { PdfUrl = pdfUrl, DiscoveredAt = DateTimeOffset.Now };
                    lock (lockObj)
                    {
                        resolved.Add(recipe);
                    }
                }

                progress?.Report(new DiscoveryProgress(
                    "Resolving PDFs", current, Volatile.Read(ref totalStubs), stub.Name));
            }
            finally
            {
                semaphore.Release();
            }
        }

        // Pipeline: each letter's recipe pages begin resolving the moment that
        // letter's sitemap is fetched and parsed — no barrier waiting for all 26.
        var letterTasks = _letters.Select(async letter =>
        {
            var url = string.Format(SitemapUrlTemplate, letter);
            List<Recipe> stubs;
            try
            {
                var html = await _httpClient.GetStringAsync(url, ct);
                stubs = HelloFreshScraper.ParseRecipeLinksFromSitemap(html);
            }
            catch (HttpRequestException ex)
            {
                progress?.Report(new DiscoveryProgress(
                    "Error", completed, Volatile.Read(ref totalStubs),
                    $"Failed to fetch sitemap for '{letter}': {ex.Message}"));
                return;
            }

            // Drop duplicates that already appeared under another letter.
            var fresh = new List<Recipe>(stubs.Count);
            lock (lockObj)
            {
                foreach (var stub in stubs)
                {
                    if (seen.Add(stub.SourceUrl))
                        fresh.Add(stub);
                }
            }

            Interlocked.Add(ref totalStubs, fresh.Count);
            progress?.Report(new DiscoveryProgress(
                "Resolving PDFs", completed, Volatile.Read(ref totalStubs),
                $"recipes-{letter}: {fresh.Count} found"));

            await Task.WhenAll(fresh.Select(ResolveStubAsync));
        });

        await Task.WhenAll(letterTasks);
        return resolved;
    }

    public async Task<string> DownloadRecipeAsync(
        Recipe recipe,
        string outputDirectory,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(recipe.PdfUrl))
            throw new InvalidOperationException($"No PDF URL for recipe '{recipe.Name}'.");

        Directory.CreateDirectory(outputDirectory);

        var baseName = FileNameSanitizer.Sanitize(recipe.Name);

        // Download PDF
        var pdfPath = Path.Combine(outputDirectory, baseName + ".pdf");
        await using var response = await _httpClient.GetStreamAsync(recipe.PdfUrl, ct);
        await using var fileStream = new FileStream(pdfPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.CopyToAsync(fileStream, ct);

        // Fetch the recipe page and parse structured data into JSON + HTML
        try
        {
            var html = await _httpClient.GetStringAsync(recipe.SourceUrl, ct);
            var recipeData = HelloFreshScraper.ParseRecipeDataFromPage(html, recipe.SourceUrl);

            if (recipeData is not null)
            {
                var jsonPath = Path.Combine(outputDirectory, baseName + ".json");
                var json = JsonSerializer.Serialize(recipeData, JsonOptions);
                await File.WriteAllTextAsync(jsonPath, json, ct);

                var htmlContent = RecipeHtmlGenerator.Generate(recipeData);
                var htmlPath = Path.Combine(outputDirectory, baseName + ".html");
                await File.WriteAllTextAsync(htmlPath, htmlContent, ct);
            }
        }
        catch
        {
            // Structured data extraction is best-effort; the PDF is the primary artifact
        }

        return pdfPath;
    }

}
