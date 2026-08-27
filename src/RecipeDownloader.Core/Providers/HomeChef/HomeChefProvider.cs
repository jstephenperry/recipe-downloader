using RecipeDownloader.Core.Models;
using RecipeDownloader.Core.Providers.Shared;

namespace RecipeDownloader.Core.Providers.HomeChef;

/// <summary>
/// Home Chef (homechef.com). Home Chef publishes no recipe sitemap, so the catalog is built
/// from the weekly menu listing. Its meal pages carry schema.org Recipe data as HTML
/// microdata rather than JSON-LD.
/// </summary>
public class HomeChefProvider : RecipeProviderBase
{
    private const string ProviderName = "Home Chef";
    private const string BaseUrl = "https://www.homechef.com";
    private const string MealPathPrefix = "/meals/";

    private static readonly string[] ListingPaths = ["/meals", "/our-menu"];

    public HomeChefProvider(HttpClient httpClient) : base(httpClient)
    {
    }

    public override string Name => ProviderName;

    /// <summary>
    /// The menu holds a few dozen meals at a time, so validating every candidate during
    /// discovery is cheap and keeps unparseable pages out of the catalog.
    /// </summary>
    protected override bool ValidateDuringDiscovery => true;

    protected override RecipeData? ParseRecipe(string html, string sourceUrl)
        => MicrodataRecipeParser.Parse(html, sourceUrl, ProviderName);

    protected override async Task<List<Recipe>> DiscoverCandidatesAsync(
        IProgress<DiscoveryProgress>? progress,
        CancellationToken ct)
    {
        progress?.Report(new DiscoveryProgress("Fetching menu", 0, 0, "Reading Home Chef menu listings..."));

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<Recipe>();

        foreach (var path in ListingPaths)
        {
            ct.ThrowIfCancellationRequested();

            string html;
            try
            {
                html = await HttpClient.GetStringAsync(BaseUrl + path, ct);
            }
            catch (HttpRequestException ex)
            {
                progress?.Report(new DiscoveryProgress(
                    "Error", candidates.Count, candidates.Count,
                    $"Failed to fetch {path}: {ex.Message}"));
                continue;
            }

            foreach (var recipe in HomeChefScraper.ParseMealLinks(html))
            {
                if (seen.Add(recipe.SourceUrl))
                    candidates.Add(recipe);
            }

            progress?.Report(new DiscoveryProgress(
                "Fetching menu", candidates.Count, candidates.Count,
                $"{candidates.Count} meals found"));
        }

        return candidates;
    }

}
