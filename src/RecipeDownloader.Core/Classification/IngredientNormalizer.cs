using System.Text.RegularExpressions;

namespace RecipeDownloader.Core.Classification;

/// <summary>
/// Normalizes ingredient names for reliable comparison and deduplication.
/// </summary>
public static partial class IngredientNormalizer
{
    private static readonly HashSet<string> Qualifiers = new(StringComparer.OrdinalIgnoreCase)
    {
        "boneless", "skinless", "fresh", "dried", "frozen", "organic",
        "large", "small", "medium", "thin", "thick", "extra",
        "chopped", "diced", "sliced", "minced", "grated", "shredded",
        "whole", "raw", "cooked", "canned", "crushed", "ground",
        "baby", "ripe", "firm", "soft", "lite", "light", "low-fat",
        "hot", "cold", "warm", "sweet", "unsalted", "salted"
    };

    public static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";

        var result = name.ToLowerInvariant().Trim();

        // Remove parenthetical content like "(optional)" or "(divided)"
        result = ParenthesesRegex().Replace(result, "").Trim();

        // Remove qualifier words
        var words = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        words = words.Where(w => !Qualifiers.Contains(w)).ToArray();
        result = string.Join(' ', words);

        // Simple plurals: trailing 'es' (tomatoes→tomato) and 's' (peppers→pepper)
        result = Singularize(result);

        // Collapse whitespace
        result = WhitespaceRegex().Replace(result, " ").Trim();

        return result;
    }

    private static string Singularize(string word)
    {
        if (word.Length < 4)
            return word;

        // Handle compound names — only singularize the last word
        var spaceIdx = word.LastIndexOf(' ');
        if (spaceIdx >= 0)
        {
            var prefix = word[..spaceIdx];
            var last = word[(spaceIdx + 1)..];
            return prefix + " " + SingularizeWord(last);
        }

        return SingularizeWord(word);
    }

    private static string SingularizeWord(string word)
    {
        if (word.Length < 4)
            return word;

        // Exceptions that should not be singularized
        if (word is "hummus" or "couscous" or "asparagus" or "citrus")
            return word;

        if (word.EndsWith("oes", StringComparison.Ordinal))
            return word[..^2]; // tomatoes → tomato, potatoes → potato

        if (word.EndsWith("ies", StringComparison.Ordinal))
            return word[..^3] + "y"; // berries → berry

        if (word.EndsWith("ves", StringComparison.Ordinal))
            return word[..^3] + "f"; // halves → half

        if (word.EndsWith("ses", StringComparison.Ordinal) ||
            word.EndsWith("ches", StringComparison.Ordinal) ||
            word.EndsWith("shes", StringComparison.Ordinal))
            return word[..^2]; // sauces → sauce, peaches → peach

        if (word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal))
            return word[..^1]; // peppers → pepper

        return word;
    }

    [GeneratedRegex(@"\s*\([^)]*\)\s*")]
    private static partial Regex ParenthesesRegex();

    [GeneratedRegex(@"\s{2,}")]
    private static partial Regex WhitespaceRegex();
}
