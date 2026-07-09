using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml;
using HtmlAgilityPack;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Providers.HelloFresh;

public static class HelloFreshScraper
{
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

        // Absolute URL to a recipe page
        if (Uri.TryCreate(href, UriKind.Absolute, out var absolute))
        {
            return absolute.AbsolutePath.StartsWith(RecipePathPrefix, StringComparison.OrdinalIgnoreCase)
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
                if (Uri.TryCreate(href, UriKind.Absolute, out _))
                    return href;
                if (href.StartsWith('/'))
                    return BaseUrl + href;
                return href;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts recipe data from a HelloFresh recipe page by parsing the
    /// schema.org Recipe JSON-LD embedded in a script[type="application/ld+json"] tag.
    /// </summary>
    public static RecipeData? ParseRecipeDataFromPage(string html, string sourceUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var scripts = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
        if (scripts is null)
            return null;

        foreach (var script in scripts)
        {
            var json = HtmlEntity.DeEntitize(script.InnerText).Trim();
            if (string.IsNullOrEmpty(json))
                continue;

            try
            {
                using var jsonDoc = JsonDocument.Parse(json);
                var root = jsonDoc.RootElement;

                // JSON-LD can be a single object or an array
                if (root.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in root.EnumerateArray())
                    {
                        if (IsRecipeType(el))
                            return BuildRecipeData(el, sourceUrl);
                    }
                }
                else if (root.ValueKind == JsonValueKind.Object && IsRecipeType(root))
                {
                    return BuildRecipeData(root, sourceUrl);
                }
            }
            catch
            {
                // Invalid JSON — try next script tag
            }
        }

        return null;
    }

    private static bool IsRecipeType(JsonElement el)
    {
        if (!el.TryGetProperty("@type", out var typeEl))
            return false;

        var type = typeEl.GetString();
        return string.Equals(type, "Recipe", StringComparison.OrdinalIgnoreCase);
    }

    private static RecipeData BuildRecipeData(JsonElement el, string sourceUrl)
    {
        var recipe = new RecipeData
        {
            Provider = "HelloFresh",
            SourceUrl = sourceUrl,
            ScrapedAt = DateTimeOffset.Now,
            Title = GetString(el, "name") ?? "Unknown Recipe",
            Description = GetString(el, "description"),
            TotalCookTimeMinutes = ParseIsoDuration(GetString(el, "totalTime")),
            ActiveCookTimeMinutes = ParseIsoDuration(GetString(el, "prepTime")),
            ServingsDisplay = GetString(el, "recipeYield"),
            CuisineType = GetString(el, "recipeCuisine"),
            Difficulty = GetString(el, "estimatedCost"), // HelloFresh sometimes uses this for difficulty
        };

        // Image — can be a string, array of strings, or ImageObject
        recipe.FeaturedImageUrl = ParseImageUrl(el);

        // Keywords → Tags
        var keywords = GetString(el, "keywords");
        if (!string.IsNullOrWhiteSpace(keywords))
        {
            foreach (var kw in keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                recipe.Tags.Add(kw);
        }

        // Category
        var category = GetString(el, "recipeCategory");
        if (!string.IsNullOrWhiteSpace(category) && !recipe.Tags.Contains(category, StringComparer.OrdinalIgnoreCase))
            recipe.Tags.Insert(0, category);

        // Ingredients — array of strings like "12 oz Boneless Chicken Thighs"
        if (el.TryGetProperty("recipeIngredient", out var ingredients) && ingredients.ValueKind == JsonValueKind.Array)
        {
            foreach (var ing in ingredients.EnumerateArray())
            {
                var text = ing.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                var parsed = ParseIngredientText(text);
                recipe.Ingredients.Add(parsed);
            }
        }

        // Instructions — array of HowToStep objects or strings
        if (el.TryGetProperty("recipeInstructions", out var instructions) && instructions.ValueKind == JsonValueKind.Array)
        {
            var stepNumber = 1;
            foreach (var step in instructions.EnumerateArray())
            {
                string? text = null;
                string? imageUrl = null;
                string? title = null;

                if (step.ValueKind == JsonValueKind.String)
                {
                    text = step.GetString();
                }
                else if (step.ValueKind == JsonValueKind.Object)
                {
                    text = GetString(step, "text");
                    title = GetString(step, "name");
                    imageUrl = ParseImageUrl(step);
                }

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                recipe.Steps.Add(new RecipeStep
                {
                    StepNumber = stepNumber++,
                    Title = title,
                    Instruction = StripHtml(text),
                    ImageUrl = imageUrl
                });
            }
        }

        // Nutrition
        if (el.TryGetProperty("nutrition", out var nutrition) && nutrition.ValueKind == JsonValueKind.Object)
        {
            var n = new RecipeNutrition
            {
                CaloriesPerServing = ParseNutrientInt(GetString(nutrition, "calories")),
                ProteinGrams = ParseNutrientInt(GetString(nutrition, "proteinContent")),
                FatGrams = ParseNutrientInt(GetString(nutrition, "fatContent")),
                CarbGrams = ParseNutrientInt(GetString(nutrition, "carbohydrateContent")),
                FiberGrams = ParseNutrientInt(GetString(nutrition, "fiberContent")),
                SodiumMg = ParseNutrientInt(GetString(nutrition, "sodiumContent")),
                SugarGrams = ParseNutrientInt(GetString(nutrition, "sugarContent")),
            };

            if (n.CaloriesPerServing.HasValue || n.ProteinGrams.HasValue || n.FatGrams.HasValue)
                recipe.Nutrition = n;
        }

        return recipe;
    }

    /// <summary>
    /// Parses an ISO 8601 duration like "PT30M" or "PT1H15M" into total minutes.
    /// </summary>
    private static int? ParseIsoDuration(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso))
            return null;

        var match = Regex.Match(iso, @"PT(?:(\d+)H)?(?:(\d+)M)?");
        if (!match.Success)
            return null;

        var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value) : 0;
        var minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value) : 0;
        var total = hours * 60 + minutes;
        return total > 0 ? total : null;
    }

    /// <summary>
    /// Parses a nutrient string like "650 calories" or "28g" into an integer value.
    /// </summary>
    private static int? ParseNutrientInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = Regex.Match(value, @"(\d+)");
        if (match.Success && int.TryParse(match.Groups[1].Value, out var result) && result > 0)
            return result;

        return null;
    }

    /// <summary>
    /// Extracts an image URL from a JSON-LD "image" property which may be
    /// a string, an array of strings, or an ImageObject with a "url" property.
    /// </summary>
    private static string? ParseImageUrl(JsonElement el)
    {
        if (!el.TryGetProperty("image", out var img))
            return null;

        if (img.ValueKind == JsonValueKind.String)
            return img.GetString();

        if (img.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in img.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    return item.GetString();
                if (item.ValueKind == JsonValueKind.Object)
                    return GetString(item, "url");
            }
        }

        if (img.ValueKind == JsonValueKind.Object)
            return GetString(img, "url");

        return null;
    }

    /// <summary>
    /// Parses a raw ingredient string like "1 unit Lemon" or "12 ounce Chicken Breast"
    /// into a structured RecipeIngredient with quantity, unit, and name.
    /// </summary>
    private static RecipeIngredient ParseIngredientText(string text)
    {
        var ingredient = new RecipeIngredient { DisplayText = text };

        // Try to extract quantity and unit from patterns like "1 unit Lemon", "12 ounce Chicken"
        var match = Regex.Match(text, @"^([\d./½¼¾⅓⅔⅛]+)\s+(unit|ounce|oz|tablespoon|tbsp|teaspoon|tsp|cup|clove|pound|lb|bunch|head|stalk|piece|pinch|dash|can|jar|package|bag|slice|strip|sprig|leaf|leaves)\s+(.+)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            ingredient.Quantity = match.Groups[1].Value;
            ingredient.Unit = match.Groups[2].Value.ToLowerInvariant();
            ingredient.Name = match.Groups[3].Value.Trim();
        }
        else
        {
            // Fallback: try just a leading number
            var simpleMatch = Regex.Match(text, @"^([\d./½¼¾⅓⅔⅛]+)\s+(.+)$");
            if (simpleMatch.Success)
            {
                ingredient.Quantity = simpleMatch.Groups[1].Value;
                ingredient.Name = simpleMatch.Groups[2].Value.Trim();
            }
            else
            {
                ingredient.Name = text;
            }
        }

        return ingredient;
    }

    private static string? GetString(JsonElement el, string prop)
    {
        if (el.ValueKind == JsonValueKind.Undefined) return null;
        return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }

    /// <summary>
    /// Converts HTML-formatted text to clean plain text.
    /// </summary>
    private static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return html;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var text = doc.DocumentNode.InnerText;
        text = System.Net.WebUtility.HtmlDecode(text);
        text = Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }
}
