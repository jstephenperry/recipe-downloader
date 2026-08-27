using System.Text.Json;
using HtmlAgilityPack;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Providers.Shared;

/// <summary>
/// Parses schema.org <c>Recipe</c> data published as JSON-LD in
/// <c>&lt;script type="application/ld+json"&gt;</c> tags.
/// </summary>
/// <remarks>
/// Google requires this markup for recipe rich results, so most commercial recipe sites
/// publish it. Any provider whose pages carry it can reuse this parser unchanged.
/// </remarks>
public static class JsonLdRecipeParser
{
    /// <summary>
    /// Extracts the first schema.org Recipe found on the page, or null when the page has none.
    /// </summary>
    public static RecipeData? Parse(string html, string sourceUrl, string providerName)
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
                var recipe = FindRecipe(jsonDoc.RootElement);
                if (recipe.HasValue)
                    return Build(recipe.Value, sourceUrl, providerName);
            }
            catch (JsonException)
            {
                // Invalid JSON in one block should not stop the others from being tried.
            }
        }

        return null;
    }

    /// <summary>
    /// Locates a Recipe node in a JSON-LD document, which may be a bare object, an array of
    /// nodes, or a <c>@graph</c> container.
    /// </summary>
    private static JsonElement? FindRecipe(JsonElement root)
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var el in root.EnumerateArray())
                {
                    var found = FindRecipe(el);
                    if (found.HasValue)
                        return found;
                }
                return null;

            case JsonValueKind.Object:
                if (IsRecipeType(root))
                    return root;

                if (root.TryGetProperty("@graph", out var graph))
                    return FindRecipe(graph);

                return null;

            default:
                return null;
        }
    }

    /// <summary>
    /// Tests the <c>@type</c> property, which may be a single string or an array of types.
    /// </summary>
    private static bool IsRecipeType(JsonElement el)
    {
        if (!el.TryGetProperty("@type", out var typeEl))
            return false;

        if (typeEl.ValueKind == JsonValueKind.String)
            return string.Equals(typeEl.GetString(), "Recipe", StringComparison.OrdinalIgnoreCase);

        if (typeEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in typeEl.EnumerateArray())
            {
                if (t.ValueKind == JsonValueKind.String &&
                    string.Equals(t.GetString(), "Recipe", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

    private static RecipeData Build(JsonElement el, string sourceUrl, string providerName)
    {
        var recipe = new RecipeData
        {
            Provider = providerName,
            SourceUrl = sourceUrl,
            ScrapedAt = DateTimeOffset.Now,
            Title = RecipeValueParser.GetString(el, "name") ?? "Unknown Recipe",
            Description = RecipeValueParser.GetString(el, "description"),
            TotalCookTimeMinutes = RecipeValueParser.ParseIsoDuration(RecipeValueParser.GetString(el, "totalTime")),
            ActiveCookTimeMinutes = RecipeValueParser.ParseIsoDuration(RecipeValueParser.GetString(el, "prepTime")),
            ServingsDisplay = RecipeValueParser.GetScalarString(el, "recipeYield"),
            CuisineType = RecipeValueParser.GetScalarString(el, "recipeCuisine"),
            FeaturedImageUrl = ParseImageUrl(el)
        };

        foreach (var keyword in RecipeValueParser.SplitKeywords(RecipeValueParser.GetScalarString(el, "keywords")))
            recipe.Tags.Add(keyword);

        var category = RecipeValueParser.GetScalarString(el, "recipeCategory");
        if (!string.IsNullOrWhiteSpace(category) &&
            !recipe.Tags.Contains(category, StringComparer.OrdinalIgnoreCase))
            recipe.Tags.Insert(0, category);

        if (el.TryGetProperty("recipeIngredient", out var ingredients) &&
            ingredients.ValueKind == JsonValueKind.Array)
        {
            foreach (var ing in ingredients.EnumerateArray())
            {
                var text = ing.GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                    recipe.Ingredients.Add(RecipeValueParser.ParseIngredientText(text));
            }
        }

        if (el.TryGetProperty("recipeInstructions", out var instructions))
            AddSteps(recipe, instructions);

        if (el.TryGetProperty("nutrition", out var nutrition) && nutrition.ValueKind == JsonValueKind.Object)
        {
            var n = new RecipeNutrition
            {
                CaloriesPerServing = RecipeValueParser.ParseNutrientInt(RecipeValueParser.GetString(nutrition, "calories")),
                ProteinGrams = RecipeValueParser.ParseNutrientInt(RecipeValueParser.GetString(nutrition, "proteinContent")),
                FatGrams = RecipeValueParser.ParseNutrientInt(RecipeValueParser.GetString(nutrition, "fatContent")),
                CarbGrams = RecipeValueParser.ParseNutrientInt(RecipeValueParser.GetString(nutrition, "carbohydrateContent")),
                FiberGrams = RecipeValueParser.ParseNutrientInt(RecipeValueParser.GetString(nutrition, "fiberContent")),
                SodiumMg = RecipeValueParser.ParseNutrientInt(RecipeValueParser.GetString(nutrition, "sodiumContent")),
                SugarGrams = RecipeValueParser.ParseNutrientInt(RecipeValueParser.GetString(nutrition, "sugarContent")),
            };

            if (n.CaloriesPerServing.HasValue || n.ProteinGrams.HasValue || n.FatGrams.HasValue)
                recipe.Nutrition = n;
        }

        return recipe;
    }

    /// <summary>
    /// Adds instruction steps. Instructions may be a plain string, an array of strings, an array
    /// of HowToStep objects, or HowToSection objects that nest their own steps.
    /// </summary>
    private static void AddSteps(RecipeData recipe, JsonElement instructions, string? sectionTitle = null)
    {
        switch (instructions.ValueKind)
        {
            case JsonValueKind.String:
                AddStep(recipe, instructions.GetString(), sectionTitle, null);
                break;

            case JsonValueKind.Array:
                foreach (var step in instructions.EnumerateArray())
                    AddSteps(recipe, step, sectionTitle);
                break;

            case JsonValueKind.Object:
                var type = RecipeValueParser.GetString(instructions, "@type");
                if (string.Equals(type, "HowToSection", StringComparison.OrdinalIgnoreCase) &&
                    instructions.TryGetProperty("itemListElement", out var nested))
                {
                    AddSteps(recipe, nested, RecipeValueParser.GetString(instructions, "name") ?? sectionTitle);
                    break;
                }

                AddStep(
                    recipe,
                    RecipeValueParser.GetString(instructions, "text"),
                    RecipeValueParser.GetString(instructions, "name") ?? sectionTitle,
                    ParseImageUrl(instructions));
                break;
        }
    }

    private static void AddStep(RecipeData recipe, string? text, string? title, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        recipe.Steps.Add(new RecipeStep
        {
            StepNumber = recipe.Steps.Count + 1,
            Title = title,
            Instruction = RecipeValueParser.StripHtml(text),
            ImageUrl = imageUrl
        });
    }

    /// <summary>
    /// Extracts an image URL from an <c>image</c> property, which may be a string,
    /// an array of strings, or an ImageObject with a <c>url</c> property.
    /// </summary>
    internal static string? ParseImageUrl(JsonElement el)
    {
        if (!el.TryGetProperty("image", out var img))
            return null;

        switch (img.ValueKind)
        {
            case JsonValueKind.String:
                return img.GetString();

            case JsonValueKind.Array:
                foreach (var item in img.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        return item.GetString();
                    if (item.ValueKind == JsonValueKind.Object)
                        return RecipeValueParser.GetString(item, "url");
                }
                return null;

            case JsonValueKind.Object:
                return RecipeValueParser.GetString(img, "url");

            default:
                return null;
        }
    }
}
