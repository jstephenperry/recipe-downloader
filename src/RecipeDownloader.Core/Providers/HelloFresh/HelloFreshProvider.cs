using System.Text.Json;
using RecipeDownloader.Core.Export;
using RecipeDownloader.Core.Models;

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
        // Fetch all sitemaps in parallel
        progress?.Report(new DiscoveryProgress("Fetching sitemaps", 0, _letters.Length));

        var sitemapTasks = _letters.Select(async letter =>
        {
            var url = string.Format(SitemapUrlTemplate, letter);
            try
            {
                var html = await _httpClient.GetStringAsync(url, ct);
                var stubs = HelloFreshScraper.ParseRecipeLinksFromSitemap(html);
                progress?.Report(new DiscoveryProgress("Fetching sitemaps", 0, _letters.Length, $"recipes-{letter}: {stubs.Count} found"));
                return stubs;
            }
            catch (HttpRequestException ex)
            {
                progress?.Report(new DiscoveryProgress("Error", 0, 0, $"Failed to fetch sitemap for '{letter}': {ex.Message}"));
                return new List<Recipe>();
            }
        });

        var sitemapResults = await Task.WhenAll(sitemapTasks);
        var allStubs = sitemapResults.SelectMany(s => s).ToList();

        progress?.Report(new DiscoveryProgress("Resolving PDFs", 0, allStubs.Count, $"Found {allStubs.Count} recipes total"));

        // Resolve all PDF URLs in parallel across all letters
        return await ResolveRecipePdfUrlsAsync(allStubs, progress, ct);
    }

    public async Task<string> DownloadRecipeAsync(
        Recipe recipe,
        string outputDirectory,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(recipe.PdfUrl))
            throw new InvalidOperationException($"No PDF URL for recipe '{recipe.Name}'.");

        Directory.CreateDirectory(outputDirectory);

        var baseName = SanitizeFileName(recipe.Name);

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

    private async Task<List<Recipe>> ResolveRecipePdfUrlsAsync(
        List<Recipe> stubs,
        IProgress<DiscoveryProgress>? progress,
        CancellationToken ct)
    {
        var semaphore = new SemaphoreSlim(MaxConcurrency);
        var resolved = new List<Recipe>();
        var lockObj = new object();
        var completed = 0;

        var tasks = stubs.Select(async stub =>
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

                progress?.Report(new DiscoveryProgress("Resolving PDFs", current, stubs.Count, stub.Name));
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
        return resolved;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", name.Select(c => invalid.Contains(c) ? '_' : c));
        if (sanitized.Length > 200)
            sanitized = sanitized[..200];
        return sanitized;
    }
}
