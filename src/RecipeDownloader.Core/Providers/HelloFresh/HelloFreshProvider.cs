using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Providers.HelloFresh;

public class HelloFreshProvider : IRecipeProvider
{
    private const string SitemapUrlTemplate = "https://www.hellofresh.com/pages/sitemap/recipes-{0}";
    private const int MaxConcurrency = 4;
    private static readonly TimeSpan ThrottleDelay = TimeSpan.FromMilliseconds(300);

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
        var allRecipes = new List<Recipe>();

        foreach (var letter in _letters)
        {
            ct.ThrowIfCancellationRequested();

            var url = string.Format(SitemapUrlTemplate, letter);
            progress?.Report(new DiscoveryProgress("Fetching sitemap", 0, 0, $"recipes-{letter}"));

            string html;
            try
            {
                html = await _httpClient.GetStringAsync(url, ct);
            }
            catch (HttpRequestException ex)
            {
                progress?.Report(new DiscoveryProgress("Error", 0, 0, $"Failed to fetch sitemap for '{letter}': {ex.Message}"));
                continue;
            }

            var stubs = HelloFreshScraper.ParseRecipeLinksFromSitemap(html);
            progress?.Report(new DiscoveryProgress("Resolving PDFs", 0, stubs.Count, $"Found {stubs.Count} recipes for '{letter}'"));

            var resolved = await ResolveRecipePdfUrlsAsync(stubs, progress, ct);
            allRecipes.AddRange(resolved);
        }

        return allRecipes;
    }

    public async Task<string> DownloadRecipeAsync(
        Recipe recipe,
        string outputDirectory,
        CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(recipe.PdfUrl))
            throw new InvalidOperationException($"No PDF URL for recipe '{recipe.Name}'.");

        Directory.CreateDirectory(outputDirectory);

        var fileName = SanitizeFileName(recipe.Name) + ".pdf";
        var filePath = Path.Combine(outputDirectory, fileName);

        await using var response = await _httpClient.GetStreamAsync(recipe.PdfUrl, ct);
        await using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await response.CopyToAsync(fileStream, ct);

        return filePath;
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
                await Task.Delay(ThrottleDelay, ct);

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
