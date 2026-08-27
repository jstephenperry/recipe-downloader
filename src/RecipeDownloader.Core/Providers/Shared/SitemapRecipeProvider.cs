using System.Globalization;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Providers.Shared;

/// <summary>
/// A <see cref="RecipeProviderBase"/> whose candidate recipe pages come from the provider's
/// sitemap. Adding a provider that publishes a sitemap needs only the sitemap URL, a URL
/// filter, and a page parser.
/// </summary>
public abstract class SitemapRecipeProvider : RecipeProviderBase
{
    protected SitemapRecipeProvider(HttpClient httpClient) : base(httpClient)
    {
    }

    /// <summary>
    /// The sitemap to read. May be a <c>&lt;urlset&gt;</c> or a <c>&lt;sitemapindex&gt;</c>.
    /// </summary>
    protected abstract string SitemapUrl { get; }

    /// <summary>
    /// Decides whether a sitemap URL is a recipe page rather than a marketing or category page.
    /// </summary>
    protected abstract bool IsRecipeUrl(string url);

    /// <summary>
    /// Narrows which child sitemaps of an index are fetched. Returning false for the
    /// irrelevant ones avoids downloading a site's entire blog archive.
    /// </summary>
    protected virtual bool IsRelevantChildSitemap(string sitemapUrl) => true;

    protected override async Task<List<Recipe>> DiscoverCandidatesAsync(
        IProgress<DiscoveryProgress>? progress,
        CancellationToken ct)
    {
        progress?.Report(new DiscoveryProgress("Fetching sitemap", 0, 0, SitemapUrl));

        List<string> locations;
        try
        {
            locations = await SitemapReader.FetchLocationsAsync(
                HttpClient, SitemapUrl, IsRelevantChildSitemap, ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or System.Xml.XmlException)
        {
            progress?.Report(new DiscoveryProgress("Error", 0, 0, $"Failed to read sitemap: {ex.Message}"));
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates = new List<Recipe>();

        foreach (var url in locations)
        {
            if (!IsRecipeUrl(url) || !seen.Add(url))
                continue;

            candidates.Add(new Recipe(DeriveName(url), url));
        }

        progress?.Report(new DiscoveryProgress(
            "Fetching sitemap", candidates.Count, candidates.Count,
            $"{candidates.Count} recipe URLs in sitemap"));

        return candidates;
    }

    /// <summary>
    /// Produces a provisional display name from the URL slug. The real title replaces it once
    /// the page is parsed during validation.
    /// </summary>
    protected virtual string DeriveName(string url)
    {
        var slug = url.TrimEnd('/').Split('/').LastOrDefault() ?? url;
        slug = slug.Split('?', '#')[0].Replace('-', ' ').Replace('_', ' ').Trim();

        return slug.Length == 0
            ? url
            : CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slug);
    }
}
