using RecipeDownloader.Core.Classification;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Matching;

/// <summary>
/// Finds recipe pairs across two protein selections that maximize ancillary ingredient overlap.
/// </summary>
public class RecipeMatcher
{
    // Category weights: vegetable/starch overlaps are more valuable than seasoning overlaps
    private static readonly Dictionary<IngredientCategory, double> CategoryWeights = new()
    {
        [IngredientCategory.Vegetable] = 3.0,
        [IngredientCategory.Starch] = 3.0,
        [IngredientCategory.Dairy] = 2.0,
        [IngredientCategory.Seasoning] = 0.5,
        [IngredientCategory.Other] = 1.0
    };

    public IReadOnlyList<RecipePairMatch> FindBestPairs(
        IEnumerable<RecipeData> allRecipes,
        string protein1,
        string protein2,
        PantryInventory? pantry = null,
        int maxResults = 10)
    {
        var recipes = allRecipes.ToList();

        var pool1 = recipes.Where(r => RecipeContainsProtein(r, protein1)).ToList();
        var pool2 = recipes.Where(r => RecipeContainsProtein(r, protein2)).ToList();

        var results = new List<RecipePairMatch>();

        foreach (var r1 in pool1)
        {
            var r1Ancillary = GetAncillaryIngredients(r1);

            foreach (var r2 in pool2)
            {
                // Skip if same recipe
                if (ReferenceEquals(r1, r2) ||
                    r1.SourceUrl == r2.SourceUrl)
                    continue;

                var r2Ancillary = GetAncillaryIngredients(r2);

                var shared = r1Ancillary.Keys
                    .Where(k => r2Ancillary.ContainsKey(k))
                    .ToList();

                var union = r1Ancillary.Keys.Union(r2Ancillary.Keys).ToList();

                if (union.Count == 0)
                    continue;

                // Weighted overlap score
                double weightedShared = shared.Sum(k =>
                {
                    var cat = r1Ancillary.TryGetValue(k, out var c1) ? c1 :
                              r2Ancillary.TryGetValue(k, out var c2) ? c2 :
                              IngredientCategory.Other;
                    return CategoryWeights.GetValueOrDefault(cat, 1.0);
                });

                double weightedUnion = union.Sum(k =>
                {
                    var cat = r1Ancillary.TryGetValue(k, out var c1) ? c1 :
                              r2Ancillary.TryGetValue(k, out var c2) ? c2 :
                              IngredientCategory.Other;
                    return CategoryWeights.GetValueOrDefault(cat, 1.0);
                });

                var score = weightedShared / weightedUnion;

                // Boost score if pantry covers unique ingredients
                if (pantry is not null)
                {
                    var uniqueItems = union.Except(shared).ToList();
                    var coveredByPantry = uniqueItems.Count(i => pantry.Contains(i));
                    if (uniqueItems.Count > 0)
                        score += 0.1 * ((double)coveredByPantry / uniqueItems.Count);
                }

                var uniqueToR1 = r1Ancillary.Keys.Except(r2Ancillary.Keys).OrderBy(x => x).ToList();
                var uniqueToR2 = r2Ancillary.Keys.Except(r1Ancillary.Keys).OrderBy(x => x).ToList();

                results.Add(new RecipePairMatch(
                    r1, r2,
                    shared.OrderBy(x => x).ToList(),
                    uniqueToR1,
                    uniqueToR2,
                    Math.Round(score, 4)));
            }
        }

        return results
            .OrderByDescending(m => m.OverlapScore)
            .ThenByDescending(m => m.SharedIngredients.Count)
            .Take(maxResults)
            .ToList();
    }

    private static bool RecipeContainsProtein(RecipeData recipe, string canonicalProtein)
    {
        var aggregator = new ProteinAggregator();
        var proteins = aggregator.GetAvailableProteins(new[] { recipe });
        return proteins.Any(p =>
            p.CanonicalName.Equals(canonicalProtein, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, IngredientCategory> GetAncillaryIngredients(RecipeData recipe)
    {
        var result = new Dictionary<string, IngredientCategory>(StringComparer.OrdinalIgnoreCase);

        foreach (var ingredient in recipe.Ingredients)
        {
            var category = IngredientClassifier.Classify(ingredient);
            if (category == IngredientCategory.Protein)
                continue;

            var normalized = IngredientNormalizer.Normalize(
                ingredient.Name ?? ingredient.DisplayText ?? "");

            if (!string.IsNullOrEmpty(normalized))
                result.TryAdd(normalized, category);
        }

        return result;
    }
}
