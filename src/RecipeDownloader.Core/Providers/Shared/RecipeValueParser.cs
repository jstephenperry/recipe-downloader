using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Providers.Shared;

/// <summary>
/// Field-level parsing helpers shared by every provider: durations, nutrient strings,
/// free-text ingredient lines, and HTML-to-text conversion.
/// </summary>
public static partial class RecipeValueParser
{
    /// <summary>
    /// Parses an ISO 8601 duration such as "PT30M" or "PT1H15M" into total minutes.
    /// Returns null for absent, malformed, or zero-length durations.
    /// </summary>
    public static int? ParseIsoDuration(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso))
            return null;

        var match = IsoDurationRegex().Match(iso);
        if (!match.Success)
            return null;

        var hours = match.Groups[1].Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
        var minutes = match.Groups[2].Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : 0;
        var total = hours * 60 + minutes;
        return total > 0 ? total : null;
    }

    /// <summary>
    /// Pulls the leading integer out of a nutrient string such as "650 calories" or "28 g".
    /// </summary>
    public static int? ParseNutrientInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var match = LeadingIntRegex().Match(value);
        return match.Success
            && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            && result > 0
                ? result
                : null;
    }

    /// <summary>
    /// Splits a schema.org "keywords" value on commas.
    /// </summary>
    public static IEnumerable<string> SplitKeywords(string? keywords)
    {
        if (string.IsNullOrWhiteSpace(keywords))
            return [];

        return keywords.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Parses a raw ingredient line such as "1 unit Lemon" or "12 ounce Chicken Breast"
    /// into quantity, unit, and name. Falls back to a bare name when no quantity is present.
    /// </summary>
    public static RecipeIngredient ParseIngredientText(string text)
    {
        var ingredient = new RecipeIngredient { DisplayText = text };

        var match = QuantityUnitNameRegex().Match(text);
        if (match.Success)
        {
            ingredient.Quantity = match.Groups[1].Value;
            ingredient.Unit = match.Groups[2].Value.ToLowerInvariant();
            ingredient.Name = match.Groups[3].Value.Trim();
            return ingredient;
        }

        var simpleMatch = QuantityNameRegex().Match(text);
        if (simpleMatch.Success)
        {
            ingredient.Quantity = simpleMatch.Groups[1].Value;
            ingredient.Name = simpleMatch.Groups[2].Value.Trim();
            return ingredient;
        }

        ingredient.Name = text;
        return ingredient;
    }

    /// <summary>
    /// Converts HTML-formatted text into clean single-spaced plain text.
    /// </summary>
    public static string StripHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return html;

        // InnerText concatenates adjacent blocks with no separator, which would run
        // "…on a plate.</p><p>Halve tomatoes." together as "…on a plate.Halve tomatoes.".
        // Inserting a space at block boundaries first keeps sentences apart.
        var spaced = BlockBoundaryRegex().Replace(html, " ");

        var doc = new HtmlDocument();
        doc.LoadHtml(spaced);
        var text = System.Net.WebUtility.HtmlDecode(doc.DocumentNode.InnerText);
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    /// <summary>
    /// Reads a string property, returning null when the property is absent or not a string.
    /// </summary>
    public static string? GetString(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object)
            return null;

        return el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }

    /// <summary>
    /// Reads a property that schema.org allows to be a string, a number, or an array of either
    /// (for example <c>recipeYield</c>), returning the first scalar as a string.
    /// </summary>
    public static string? GetScalarString(JsonElement el, string prop)
    {
        if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(prop, out var v))
            return null;

        return ScalarToString(v);
    }

    private static string? ScalarToString(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString(),
        JsonValueKind.Number => v.ToString(),
        JsonValueKind.Array => v.EnumerateArray()
            .Select(ScalarToString)
            .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)),
        _ => null
    };

    [GeneratedRegex(@"PT(?:(\d+)H)?(?:(\d+)M)?")]
    private static partial Regex IsoDurationRegex();

    [GeneratedRegex(@"(\d+)")]
    private static partial Regex LeadingIntRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"<\s*/?\s*(br|p|div|li|ul|ol|tr|td|h[1-6]|section|article)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockBoundaryRegex();

    [GeneratedRegex(@"^([\d./½¼¾⅓⅔⅛]+)\s+(unit|ounce|oz|tablespoon|tbsp|teaspoon|tsp|cup|clove|pound|lb|bunch|head|stalk|piece|pinch|dash|can|jar|package|bag|slice|strip|sprig|leaf|leaves)\s+(.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex QuantityUnitNameRegex();

    [GeneratedRegex(@"^([\d./½¼¾⅓⅔⅛]+)\s+(.+)$")]
    private static partial Regex QuantityNameRegex();
}
