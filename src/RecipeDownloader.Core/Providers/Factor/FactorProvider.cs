using System.Text.RegularExpressions;
using RecipeDownloader.Core.Models;
using RecipeDownloader.Core.Providers.Shared;

namespace RecipeDownloader.Core.Providers.Factor;

/// <summary>
/// Factor (factor75.com), a HelloFresh Group brand. Its recipe pages run on the same
/// Next.js platform as HelloFresh, so they carry the platform's structured recipe payload.
/// </summary>
public partial class FactorProvider : SitemapRecipeProvider
{
    private const string ProviderName = "Factor";
    private const string RecipePathFragment = "/recipes/";

    public FactorProvider(HttpClient httpClient) : base(httpClient)
    {
    }

    public override string Name => ProviderName;

    protected override string SitemapUrl => "https://www.factor75.com/sitemap_index.xml";

    /// <summary>
    /// Factor publishes roughly nine thousand recipe pages, each around a megabyte. Visiting
    /// every one during discovery would move gigabytes for no benefit, so the catalog is built
    /// from the sitemap alone and each page is parsed when the user downloads it.
    /// </summary>
    protected override bool ValidateDuringDiscovery => false;

    protected override bool IsRelevantChildSitemap(string sitemapUrl)
        => sitemapUrl.Contains("recipe", StringComparison.OrdinalIgnoreCase);

    protected override bool IsRecipeUrl(string url)
        => url.Contains(RecipePathFragment, StringComparison.OrdinalIgnoreCase)
           && !url.EndsWith(RecipePathFragment, StringComparison.OrdinalIgnoreCase)
           // Factor leaves internal test entries in its sitemap; they carry placeholder data.
           && !url.Contains("-test-", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Factor recipe slugs end in the recipe's 24-character hexadecimal id, which is noise
    /// in a display name.
    /// </summary>
    protected override string DeriveName(string url)
        => base.DeriveName(IdSuffixRegex().Replace(url, ""));

    protected override RecipeData? ParseRecipe(string html, string sourceUrl)
        => HelloFreshPlatformParser.Parse(html, sourceUrl, ProviderName);

    [GeneratedRegex(@"-[0-9a-f]{24}/?$", RegexOptions.IgnoreCase)]
    private static partial Regex IdSuffixRegex();
}
