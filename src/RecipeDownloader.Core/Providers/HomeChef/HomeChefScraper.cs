using HtmlAgilityPack;
using RecipeDownloader.Core.Models;
using RecipeDownloader.Core.Providers.Shared;

namespace RecipeDownloader.Core.Providers.HomeChef;

public static class HomeChefScraper
{
    private const string BaseUrl = "https://www.homechef.com";
    private const string MealPathPrefix = "/meals/";

    /// <summary>
    /// Collects links to individual meal pages from a menu listing.
    /// </summary>
    public static List<Recipe> ParseMealLinks(string html)
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
            var url = NormalizeMealUrl(link.GetAttributeValue("href", ""));
            if (url is null || !seen.Add(url))
                continue;

            var name = RecipeValueParser.StripHtml(HtmlEntity.DeEntitize(link.InnerText));
            if (string.IsNullOrWhiteSpace(name))
                name = DeriveNameFromUrl(url);

            recipes.Add(new Recipe(name, url));
        }

        return recipes;
    }

    /// <summary>
    /// Converts a meal href to an absolute URL, or returns null when it is not a meal page.
    /// </summary>
    private static string? NormalizeMealUrl(string href)
    {
        if (string.IsNullOrWhiteSpace(href))
            return null;

        string path;

        if (IsHttpUrl(href, out var absolute))
        {
            if (!absolute!.Host.EndsWith("homechef.com", StringComparison.OrdinalIgnoreCase))
                return null;

            path = absolute.AbsolutePath;
        }
        else if (href.StartsWith('/'))
        {
            path = href.Split('?', '#')[0];
        }
        else
        {
            return null;
        }

        if (!path.StartsWith(MealPathPrefix, StringComparison.OrdinalIgnoreCase) ||
            path.Length <= MealPathPrefix.Length)
            return null;

        return BaseUrl + path.TrimEnd('/');
    }

    /// <summary>
    /// Tests whether an href is an absolute http(s) URL.
    /// </summary>
    /// <remarks>
    /// The scheme check is essential rather than cosmetic: on Unix,
    /// <c>Uri.TryCreate("/meals/x", UriKind.Absolute, out _)</c> succeeds and yields
    /// <c>file:///meals/x</c>, so a bare <c>TryCreate</c> would treat every site-relative
    /// link as absolute and discard it.
    /// </remarks>
    private static bool IsHttpUrl(string href, out Uri? uri)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            return true;

        uri = null;
        return false;
    }

    private static string DeriveNameFromUrl(string url)
    {
        var slug = url.TrimEnd('/').Split('/').LastOrDefault() ?? url;
        slug = slug.Replace('-', ' ').Trim();

        return slug.Length == 0
            ? url
            : System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase(slug);
    }
}
