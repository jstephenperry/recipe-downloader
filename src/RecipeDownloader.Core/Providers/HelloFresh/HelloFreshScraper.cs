using HtmlAgilityPack;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Providers.HelloFresh;

public static class HelloFreshScraper
{
    private const string RecipeUrlPrefix = "https://www.hellofresh.com/recipes/";
    private const string PdfUrlFragment = "recipecards/card";

    public static List<Recipe> ParseRecipeLinksFromSitemap(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var recipes = new List<Recipe>();
        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        if (links is null)
            return recipes;

        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", "");
            if (!href.StartsWith(RecipeUrlPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            var name = HtmlEntity.DeEntitize(link.InnerText).Trim();
            if (name.EndsWith(" Recipe", StringComparison.OrdinalIgnoreCase))
                name = name[..^" Recipe".Length].Trim();

            if (string.IsNullOrWhiteSpace(name))
                continue;

            recipes.Add(new Recipe(name, href));
        }

        return recipes;
    }

    public static string? ParsePdfUrlFromRecipePage(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var links = doc.DocumentNode.SelectNodes("//a[@href]");
        if (links is null)
            return null;

        foreach (var link in links)
        {
            var href = link.GetAttributeValue("href", "");
            if (href.Contains(PdfUrlFragment, StringComparison.OrdinalIgnoreCase)
                && href.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return href;
            }
        }

        return null;
    }
}
