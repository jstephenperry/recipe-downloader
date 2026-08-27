using HtmlAgilityPack;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Providers.Shared;

/// <summary>
/// Parses schema.org <c>Recipe</c> data published as HTML microdata
/// (<c>itemscope</c> / <c>itemtype</c> / <c>itemprop</c> attributes).
/// </summary>
/// <remarks>
/// Microdata is the other markup Google accepts for recipe rich results. Sites that render
/// recipes server-side into HTML — Home Chef, for instance — often use it instead of JSON-LD.
/// Property lookups honour <c>itemscope</c> boundaries so that unrelated items on the page
/// (the Organization in a site header, say) cannot leak into the recipe.
/// </remarks>
public static class MicrodataRecipeParser
{
    public static RecipeData? Parse(string html, string sourceUrl, string providerName)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var root = FindItemScope(doc.DocumentNode, "Recipe");
        if (root is null)
            return null;

        var (title, subtitle) = ReadTitle(root);

        var recipe = new RecipeData
        {
            Provider = providerName,
            SourceUrl = sourceUrl,
            ScrapedAt = DateTimeOffset.Now,
            Title = title,
            Description = GetPropertyValue(root, "description"),
            ServingsDisplay = GetPropertyValue(root, "recipeYield"),
            CuisineType = GetPropertyValue(root, "recipeCuisine"),
            TotalCookTimeMinutes = RecipeValueParser.ParseIsoDuration(GetPropertyValue(root, "totalTime")),
            ActiveCookTimeMinutes = RecipeValueParser.ParseIsoDuration(GetPropertyValue(root, "prepTime")),
            FeaturedImageUrl = GetPropertyValue(root, "image"),
            Subtitle = subtitle
        };

        var category = GetPropertyValue(root, "recipeCategory");
        if (!string.IsNullOrWhiteSpace(category))
            recipe.Tags.Add(category);

        foreach (var keyword in RecipeValueParser.SplitKeywords(GetPropertyValue(root, "keywords")))
            recipe.Tags.Add(keyword);

        foreach (var node in GetPropertyNodes(root, "recipeIngredient"))
        {
            var text = CleanText(node.InnerText);
            if (!string.IsNullOrWhiteSpace(text))
                recipe.Ingredients.Add(RecipeValueParser.ParseIngredientText(text));
        }

        AddSteps(recipe, root);
        AddNutrition(recipe, root);

        return recipe;
    }

    /// <summary>
    /// Reads the recipe title, splitting off a subtitle when the <c>name</c> property wraps
    /// two headings — a pattern sites use to mark up "Dish Name" plus "with a garnish".
    /// </summary>
    private static (string Title, string? Subtitle) ReadTitle(HtmlNode root)
    {
        var node = GetPropertyNodes(root, "name").FirstOrDefault();
        if (node is null)
            return ("Unknown Recipe", null);

        var headings = node.SelectNodes(".//h1|.//h2|.//h3|.//h4");
        if (headings is not null && headings.Count >= 2)
        {
            var title = CleanText(headings[0].InnerText);
            var subtitle = CleanText(headings[1].InnerText);

            if (!string.IsNullOrWhiteSpace(title))
                return (title, string.IsNullOrWhiteSpace(subtitle) ? null : subtitle);
        }

        var value = GetPropertyValue(root, "name");
        return (string.IsNullOrWhiteSpace(value) ? "Unknown Recipe" : value, null);
    }

    private static void AddSteps(RecipeData recipe, HtmlNode root)
    {
        foreach (var instructions in GetPropertyNodes(root, "recipeInstructions"))
        {
            // Instructions are either a list of ListItem items or a single block of text.
            var items = GetPropertyNodes(instructions, "itemListElement").ToList();

            if (items.Count == 0)
            {
                var text = CleanText(instructions.InnerText);
                if (!string.IsNullOrWhiteSpace(text))
                    recipe.Steps.Add(new RecipeStep
                    {
                        StepNumber = recipe.Steps.Count + 1,
                        Instruction = text
                    });

                continue;
            }

            foreach (var item in items)
            {
                var instruction = GetPropertyValue(item, "text")
                    ?? GetPropertyValue(item, "description")
                    ?? CleanText(item.InnerText);

                if (string.IsNullOrWhiteSpace(instruction))
                    continue;

                recipe.Steps.Add(new RecipeStep
                {
                    StepNumber = recipe.Steps.Count + 1,
                    Title = GetPropertyValue(item, "name"),
                    Instruction = instruction,
                    ImageUrl = GetPropertyValue(item, "image") ?? FindImageUrl(item)
                });
            }
        }
    }

    private static void AddNutrition(RecipeData recipe, HtmlNode root)
    {
        var node = GetPropertyNodes(root, "nutrition").FirstOrDefault();
        if (node is null)
            return;

        var nutrition = new RecipeNutrition
        {
            CaloriesPerServing = RecipeValueParser.ParseNutrientInt(GetPropertyValue(node, "calories")),
            ProteinGrams = RecipeValueParser.ParseNutrientInt(GetPropertyValue(node, "proteinContent")),
            FatGrams = RecipeValueParser.ParseNutrientInt(GetPropertyValue(node, "fatContent")),
            CarbGrams = RecipeValueParser.ParseNutrientInt(GetPropertyValue(node, "carbohydrateContent")),
            FiberGrams = RecipeValueParser.ParseNutrientInt(GetPropertyValue(node, "fiberContent")),
            SodiumMg = RecipeValueParser.ParseNutrientInt(GetPropertyValue(node, "sodiumContent")),
            SugarGrams = RecipeValueParser.ParseNutrientInt(GetPropertyValue(node, "sugarContent")),
        };

        if (nutrition.CaloriesPerServing.HasValue || nutrition.ProteinGrams.HasValue || nutrition.FatGrams.HasValue)
            recipe.Nutrition = nutrition;
    }

    /// <summary>
    /// Finds the first element whose <c>itemtype</c> names the given schema.org type,
    /// accepting either the http or https form of the vocabulary URL.
    /// </summary>
    private static HtmlNode? FindItemScope(HtmlNode context, string typeName)
    {
        var nodes = context.SelectNodes(".//*[@itemscope][@itemtype]");
        if (nodes is null)
            return null;

        foreach (var node in nodes)
        {
            var itemType = node.GetAttributeValue("itemtype", "");
            var slash = itemType.LastIndexOf('/');
            var name = slash >= 0 ? itemType[(slash + 1)..] : itemType;

            if (itemType.Contains("schema.org", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(name, typeName, StringComparison.OrdinalIgnoreCase))
                return node;
        }

        return null;
    }

    /// <summary>
    /// Returns the elements carrying <paramref name="propertyName"/> that belong to
    /// <paramref name="scope"/> itself — descendants nested inside another
    /// <c>itemscope</c> belong to that inner item and are skipped.
    /// </summary>
    private static IEnumerable<HtmlNode> GetPropertyNodes(HtmlNode scope, string propertyName)
    {
        var nodes = scope.SelectNodes($".//*[@itemprop='{propertyName}']");
        if (nodes is null)
            yield break;

        foreach (var node in nodes)
        {
            if (BelongsToScope(node, scope))
                yield return node;
        }
    }

    private static bool BelongsToScope(HtmlNode node, HtmlNode scope)
    {
        for (var parent = node.ParentNode; parent is not null && parent != scope; parent = parent.ParentNode)
        {
            if (parent.Attributes.Contains("itemscope"))
                return false;
        }

        return true;
    }

    private static string? GetPropertyValue(HtmlNode scope, string propertyName)
    {
        var node = GetPropertyNodes(scope, propertyName).FirstOrDefault();
        if (node is null)
            return null;

        // The microdata spec takes the value from an attribute for certain elements,
        // and from the element's text for everything else.
        var value = node.Name.ToLowerInvariant() switch
        {
            "meta" => Attribute(node, "content"),
            "img" or "audio" or "video" or "embed" or "source" or "track"
                => Attribute(node, "src"),
            "a" or "area" or "link" => Attribute(node, "href"),
            "object" => Attribute(node, "data"),
            "data" or "meter" => Attribute(node, "value"),
            "time" => Attribute(node, "datetime") ?? node.InnerText,
            _ => node.InnerText
        };

        value = CleanText(value);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Falls back to the first image inside a step when the step has no <c>image</c> property.
    /// Lazy-loaded images keep their real URL in <c>data-src</c> rather than <c>src</c>.
    /// </summary>
    private static string? FindImageUrl(HtmlNode node)
    {
        var img = node.SelectSingleNode(".//img");
        if (img is null)
            return null;

        return Attribute(img, "data-src") ?? Attribute(img, "src");
    }

    /// <summary>
    /// Reads an attribute, treating an absent or blank attribute as null.
    /// </summary>
    private static string? Attribute(HtmlNode node, string name)
    {
        var value = node.GetAttributeValue(name, "");
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string? CleanText(string? value)
        => value is null ? null : RecipeValueParser.StripHtml(HtmlEntity.DeEntitize(value));
}
