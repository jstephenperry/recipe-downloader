using HtmlAgilityPack;
using RecipeDownloader.Core.Models;
using RecipeDownloader.Core.Providers.Shared;

namespace RecipeDownloader.Core.Providers.HelloFresh;

public static class HelloFreshScraper
{
    private const string ProviderName = "HelloFresh";
    private const string BaseUrl = "https://www.hellofresh.com";
    private const string RecipePathPrefix = "/recipes/";
    private const string PdfUrlFragment = "recipecards/card";

    public static List<Recipe> ParseRecipeLinksFromSitemap(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var recipes = new List<Recipe>();
        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        if (links is null)
            return recipes;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", "");
            var absoluteUrl = NormalizeRecipeUrl(href);
            if (absoluteUrl is null)
                continue;

            if (!seen.Add(absoluteUrl))
                continue;

            var name = HtmlEntity.DeEntitize(link.InnerText).Trim();
            if (name.EndsWith(" Recipe", StringComparison.OrdinalIgnoreCase))
                name = name[..^" Recipe".Length].Trim();

            if (string.IsNullOrWhiteSpace(name))
                continue;

            recipes.Add(new Recipe(name, absoluteUrl));
        }

        return recipes;
    }

    /// <summary>
    /// Normalizes a recipe href (which may be relative like "/recipes/foo" or
    /// absolute like "https://www.hellofresh.com/recipes/foo") to an absolute URL.
    /// Returns null if the href is not a recipe link.
    /// </summary>
    private static string? NormalizeRecipeUrl(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return null;

        // Absolute URL to a recipe page. The scheme check matters on Unix, where
        // Uri.TryCreate("/recipes/x", UriKind.Absolute, out _) succeeds as file:///recipes/x
        // and would otherwise swallow every site-relative link.
        if (IsHttpUrl(href, out var absolute))
        {
            return absolute!.AbsolutePath.StartsWith(RecipePathPrefix, StringComparison.OrdinalIgnoreCase)
                ? absolute.GetLeftPart(UriPartial.Path)
                : null;
        }

        // Relative URL starting with /recipes/
        if (href.StartsWith(RecipePathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var path = href.Split('?', '#')[0];
            return BaseUrl + path;
        }

        return null;
    }

    /// <summary>
    /// Tests whether an href is an absolute http(s) URL rather than a site-relative path.
    /// </summary>
    private static bool IsHttpUrl(string href, out Uri? uri)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return true;

        uri = null;
        return false;
    }

    /// <summary>
    /// Finds the printable recipe-card PDF for a recipe page. The link is present in the
    /// page markup on most recipes; newer pages advertise it in the platform payload instead.
    /// </summary>
    public static string? ParsePdfUrlFromRecipePage(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        if (links is not null)
        {
            foreach (var link in links)
            {
                var href = link.GetAttributeValue("href", "");
                if (href.Contains(PdfUrlFragment, StringComparison.OrdinalIgnoreCase)
                    && href.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    if (IsHttpUrl(href, out _))
                        return href;
                    if (href.StartsWith('/'))
                        return BaseUrl + href;
                    return href;
                }
            }
        }

        return HelloFreshPlatformParser.ParseCardLink(html);
    }

    /// <summary>
    /// Extracts recipe data from a HelloFresh recipe page. The platform payload is preferred
    /// because it carries per-serving ingredient amounts, allergens, and structured nutrition;
    /// the schema.org JSON-LD on the same page is the fallback.
    /// </summary>
    public static RecipeData? ParseRecipeDataFromPage(string html, string sourceUrl)
        => HelloFreshPlatformParser.Parse(html, sourceUrl, ProviderName)
           ?? JsonLdRecipeParser.Parse(html, sourceUrl, ProviderName);
}
