using RecipeDownloader.Core.Classification;
using RecipeDownloader.Core.Matching;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.GroceryList;

/// <summary>
/// Generates an optimized grocery list from a selected recipe pair, subtracting pantry items.
/// </summary>
public class GroceryListGenerator
{
    public GroceryList Generate(RecipePairMatch selectedPair, PantryInventory pantry)
    {
        // Build a merged ingredient map: normalized name -> aggregated info
        var merged = new Dictionary<string, MergedIngredient>(StringComparer.OrdinalIgnoreCase);

        AddRecipeIngredients(merged, selectedPair.Recipe1);
        AddRecipeIngredients(merged, selectedPair.Recipe2);

        var items = new List<GroceryItem>();
        var onHand = new List<GroceryItem>();
        var sharedCount = 0;

        foreach (var (name, mi) in merged)
        {
            var isShared = mi.Recipes.Count > 1;
            if (isShared) sharedCount++;

            // Combine quantities across recipes
            string? combinedQty = null;
            string? combinedUnit = null;

            if (mi.Quantities.Count == 1)
            {
                var (qty, unit) = mi.Quantities[0];
                var parsed = QuantityParser.Parse(qty);
                combinedQty = parsed.HasValue ? QuantityParser.Format(parsed.Value) : qty;
                combinedUnit = unit;
            }
            else if (mi.Quantities.Count == 2)
            {
                var (qty1, unit1) = mi.Quantities[0];
                var (qty2, unit2) = mi.Quantities[1];
                var (combined, unit) = QuantityParser.CombineQuantities(qty1, unit1, qty2, unit2);
                combinedQty = combined.HasValue ? QuantityParser.Format(combined.Value) : null;
                combinedUnit = unit;
            }

            var item = new GroceryItem
            {
                Name = mi.DisplayName,
                CombinedQuantity = combinedQty,
                Unit = combinedUnit,
                Category = mi.Category,
                IsShared = isShared,
                UsedInRecipes = mi.Recipes.ToList()
            };

            if (pantry.Contains(name))
                onHand.Add(item);
            else
                items.Add(item);
        }

        // Sort by category, then name
        var categoryOrder = new[]
        {
            IngredientCategory.Protein,
            IngredientCategory.Vegetable,
            IngredientCategory.Starch,
            IngredientCategory.Dairy,
            IngredientCategory.Seasoning,
            IngredientCategory.Other
        };

        items = items
            .OrderBy(i => Array.IndexOf(categoryOrder, i.Category))
            .ThenBy(i => i.Name)
            .ToList();

        onHand = onHand
            .OrderBy(i => Array.IndexOf(categoryOrder, i.Category))
            .ThenBy(i => i.Name)
            .ToList();

        var totalUnique = merged.Count;
        var efficiency = totalUnique > 0 ? (double)sharedCount / totalUnique : 0;

        return new GroceryList
        {
            Items = items,
            AlreadyOnHand = onHand,
            TotalUniqueIngredients = totalUnique,
            IngredientsFromPantry = onHand.Count,
            SharedBetweenRecipes = sharedCount,
            WasteEfficiency = Math.Round(efficiency, 2)
        };
    }

    private static void AddRecipeIngredients(
        Dictionary<string, MergedIngredient> merged,
        RecipeData recipe)
    {
        foreach (var ingredient in recipe.Ingredients)
        {
            var normalized = IngredientNormalizer.Normalize(
                ingredient.Name ?? ingredient.DisplayText ?? "");

            if (string.IsNullOrEmpty(normalized))
                continue;

            if (!merged.TryGetValue(normalized, out var existing))
            {
                existing = new MergedIngredient
                {
                    DisplayName = ingredient.Name ?? ingredient.DisplayText ?? normalized,
                    Category = IngredientClassifier.Classify(ingredient)
                };
                merged[normalized] = existing;
            }

            existing.Quantities.Add((ingredient.Quantity, ingredient.Unit));

            if (!existing.Recipes.Contains(recipe.Title))
                existing.Recipes.Add(recipe.Title);
        }
    }

    private class MergedIngredient
    {
        public string DisplayName { get; set; } = "";
        public IngredientCategory Category { get; set; }
        public List<(string? Quantity, string? Unit)> Quantities { get; } = [];
        public List<string> Recipes { get; } = [];
    }
}
