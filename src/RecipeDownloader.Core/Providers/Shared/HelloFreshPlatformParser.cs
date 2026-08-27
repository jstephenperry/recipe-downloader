using System.Globalization;
using System.Text.Json;
using HtmlAgilityPack;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Providers.Shared;

/// <summary>
/// Parses the recipe payload embedded by the HelloFresh Group web platform, which serves
/// HelloFresh and its sibling brands (Factor among them).
/// </summary>
/// <remarks>
/// These pages are Next.js applications that ship a React Query cache inside
/// <c>&lt;script id="__NEXT_DATA__"&gt;</c>. The cache holds a <c>recipe.byId</c> query whose
/// payload is the platform's own normalized recipe record — richer than the schema.org
/// markup on the same page, since it carries per-serving ingredient amounts, structured
/// nutrition, allergens, utensils, and the printable recipe-card link.
/// </remarks>
public static class HelloFreshPlatformParser
{
    private const string RecipeQueryPrefix = "recipe.byId";

    /// <summary>
    /// Extracts the platform recipe record from a rendered page, or null when the page
    /// carries no such payload.
    /// </summary>
    public static RecipeData? Parse(string html, string sourceUrl, string providerName)
    {
        var payload = ExtractRecipeElement(html);
        return payload.HasValue ? Build(payload.Value, sourceUrl, providerName) : null;
    }

    /// <summary>
    /// Returns the printable recipe-card PDF URL when the payload advertises one.
    /// </summary>
    public static string? ParseCardLink(string html)
    {
        var payload = ExtractRecipeElement(html);
        if (!payload.HasValue)
            return null;

        var cardLink = RecipeValueParser.GetString(payload.Value, "cardLink");
        return string.IsNullOrWhiteSpace(cardLink) ? null : cardLink;
    }

    private static JsonElement? ExtractRecipeElement(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var script = doc.DocumentNode.SelectSingleNode("//script[@id='__NEXT_DATA__']");
        if (script is null)
            return null;

        var json = HtmlEntity.DeEntitize(script.InnerText).Trim();
        if (string.IsNullOrEmpty(json))
            return null;

        JsonDocument jsonDoc;
        try
        {
            jsonDoc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        // JsonDocument owns the parsed buffer, so the element must be cloned before it is disposed.
        using (jsonDoc)
        {
            if (!TryGetPath(jsonDoc.RootElement,
                    ["props", "pageProps", "ssrPayload", "dehydratedState", "queries"], out var queries)
                || queries.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var query in queries.EnumerateArray())
            {
                if (!IsRecipeQuery(query))
                    continue;

                if (TryGetPath(query, ["state", "data"], out var data) &&
                    data.ValueKind == JsonValueKind.Object)
                    return data.Clone();
            }
        }

        return null;
    }

    private static bool IsRecipeQuery(JsonElement query)
    {
        if (!query.TryGetProperty("queryKey", out var key) || key.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var part in key.EnumerateArray())
        {
            return part.ValueKind == JsonValueKind.String
                && string.Equals(part.GetString(), RecipeQueryPrefix, StringComparison.Ordinal);
        }

        return false;
    }

    private static bool TryGetPath(JsonElement root, string[] path, out JsonElement result)
    {
        result = root;
        foreach (var segment in path)
        {
            if (result.ValueKind != JsonValueKind.Object || !result.TryGetProperty(segment, out result))
                return false;
        }

        return true;
    }

    private static RecipeData Build(JsonElement el, string sourceUrl, string providerName)
    {
        var recipe = new RecipeData
        {
            Provider = providerName,
            SourceUrl = sourceUrl,
            ScrapedAt = DateTimeOffset.Now,
            Title = RecipeValueParser.GetString(el, "name") ?? "Unknown Recipe",
            Subtitle = RecipeValueParser.GetString(el, "headline"),
            Description = RecipeValueParser.GetString(el, "description"),
            ActiveCookTimeMinutes = RecipeValueParser.ParseIsoDuration(RecipeValueParser.GetString(el, "prepTime")),
            TotalCookTimeMinutes = RecipeValueParser.ParseIsoDuration(RecipeValueParser.GetString(el, "totalTime")),
            Difficulty = DescribeDifficulty(el),
            FeaturedImageUrl = RecipeValueParser.GetString(el, "imageLink"),
            CuisineType = FirstNamed(el, "cuisines"),
            ServingsDisplay = DescribeServings(el)
        };

        recipe.Tags.AddRange(AllNamed(el, "tags"));
        recipe.Allergens.AddRange(AllNamed(el, "allergens"));

        AddIngredients(recipe, el);
        AddSteps(recipe, el);
        AddNutrition(recipe, el);

        return recipe;
    }

    /// <summary>
    /// Joins the ingredient catalog (names) with the per-yield amounts, which the payload
    /// stores separately and keys by ingredient id.
    /// </summary>
    private static void AddIngredients(RecipeData recipe, JsonElement el)
    {
        if (!el.TryGetProperty("ingredients", out var ingredients) ||
            ingredients.ValueKind != JsonValueKind.Array)
            return;

        var amounts = ReadYieldAmounts(el);

        foreach (var ingredient in ingredients.EnumerateArray())
        {
            var name = RecipeValueParser.GetString(ingredient, "name");
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var id = RecipeValueParser.GetString(ingredient, "id");
            var parsed = new RecipeIngredient { Name = name };

            if (id is not null && amounts.TryGetValue(id, out var amount))
            {
                parsed.Quantity = amount.Quantity;
                parsed.Unit = amount.Unit;
            }

            parsed.DisplayText = string.Join(" ",
                new[] { parsed.Quantity, parsed.Unit, parsed.Name }
                    .Where(part => !string.IsNullOrWhiteSpace(part)));

            recipe.Ingredients.Add(parsed);
        }
    }

    /// <summary>
    /// Reads amounts from the smallest yield (a single serving where available) so quantities
    /// are comparable across providers.
    /// </summary>
    private static Dictionary<string, (string? Quantity, string? Unit)> ReadYieldAmounts(JsonElement el)
    {
        var amounts = new Dictionary<string, (string?, string?)>(StringComparer.Ordinal);

        if (!el.TryGetProperty("yields", out var yields) || yields.ValueKind != JsonValueKind.Array)
            return amounts;

        var selected = default(JsonElement);
        var selectedServings = int.MaxValue;
        var found = false;

        foreach (var yield in yields.EnumerateArray())
        {
            var servings = yield.TryGetProperty("yields", out var y) && y.ValueKind == JsonValueKind.Number
                ? y.GetInt32()
                : int.MaxValue;

            if (servings > selectedServings)
                continue;

            selected = yield;
            selectedServings = servings;
            found = true;
        }

        if (!found || !selected.TryGetProperty("ingredients", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
            return amounts;

        foreach (var entry in entries.EnumerateArray())
        {
            var id = RecipeValueParser.GetString(entry, "id");
            if (id is null)
                continue;

            var quantity = entry.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Number
                ? a.GetDouble().ToString("0.####", CultureInfo.InvariantCulture)
                : null;

            amounts[id] = (quantity, RecipeValueParser.GetString(entry, "unit"));
        }

        return amounts;
    }

    private static void AddSteps(RecipeData recipe, JsonElement el)
    {
        if (!el.TryGetProperty("steps", out var steps) || steps.ValueKind != JsonValueKind.Array)
            return;

        foreach (var step in steps.EnumerateArray())
        {
            var instruction = RecipeValueParser.GetString(step, "instructions")
                ?? RecipeValueParser.GetString(step, "instructionsMarkdown");

            if (string.IsNullOrWhiteSpace(instruction))
                continue;

            recipe.Steps.Add(new RecipeStep
            {
                StepNumber = step.TryGetProperty("index", out var index) && index.ValueKind == JsonValueKind.Number
                    ? index.GetInt32()
                    : recipe.Steps.Count + 1,
                Instruction = RecipeValueParser.StripHtml(instruction),
                ImageUrl = FirstStepImage(step)
            });
        }
    }

    private static string? FirstStepImage(JsonElement step)
    {
        if (!step.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var image in images.EnumerateArray())
            return RecipeValueParser.GetString(image, "link");

        return null;
    }

    /// <summary>
    /// Maps the platform's nutrition list, which is an array of named amounts rather than
    /// fixed fields, onto <see cref="RecipeNutrition"/>.
    /// </summary>
    private static void AddNutrition(RecipeData recipe, JsonElement el)
    {
        if (!el.TryGetProperty("nutrition", out var nutrition) || nutrition.ValueKind != JsonValueKind.Array)
            return;

        var parsed = new RecipeNutrition();
        var any = false;

        foreach (var entry in nutrition.EnumerateArray())
        {
            var name = RecipeValueParser.GetString(entry, "name");
            if (name is null ||
                !entry.TryGetProperty("amount", out var amountEl) ||
                amountEl.ValueKind != JsonValueKind.Number)
                continue;

            var amount = (int)Math.Round(amountEl.GetDouble());

            switch (name.ToLowerInvariant())
            {
                case "calories": parsed.CaloriesPerServing = amount; any = true; break;
                case "protein": parsed.ProteinGrams = amount; any = true; break;
                case "fat": parsed.FatGrams = amount; any = true; break;
                case "carbohydrate": parsed.CarbGrams = amount; any = true; break;
                case "dietary fiber": parsed.FiberGrams = amount; any = true; break;
                case "sodium": parsed.SodiumMg = amount; any = true; break;
                case "sugar": parsed.SugarGrams = amount; any = true; break;
            }
        }

        if (any)
            recipe.Nutrition = parsed;
    }

    private static string? DescribeServings(JsonElement el)
    {
        if (!el.TryGetProperty("yields", out var yields) || yields.ValueKind != JsonValueKind.Array)
            return null;

        foreach (var yield in yields.EnumerateArray())
        {
            if (yield.TryGetProperty("yields", out var y) && y.ValueKind == JsonValueKind.Number)
                return $"{y.GetInt32()} servings";
        }

        return null;
    }

    /// <summary>
    /// The platform reports difficulty as an integer rank rather than a label.
    /// </summary>
    private static string? DescribeDifficulty(JsonElement el)
    {
        if (!el.TryGetProperty("difficulty", out var difficulty) ||
            difficulty.ValueKind != JsonValueKind.Number)
            return null;

        return difficulty.GetInt32() switch
        {
            1 => "Easy",
            2 => "Medium",
            3 => "Hard",
            var other => other.ToString(CultureInfo.InvariantCulture)
        };
    }

    private static string? FirstNamed(JsonElement el, string property)
        => AllNamed(el, property).FirstOrDefault();

    private static IEnumerable<string> AllNamed(JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var item in array.EnumerateArray())
        {
            var name = RecipeValueParser.GetString(item, "name");
            if (!string.IsNullOrWhiteSpace(name))
                yield return name;
        }
    }
}
