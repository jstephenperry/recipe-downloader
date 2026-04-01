using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Classification;

public record ProteinSummary(
    string CanonicalName,
    int RecipeCount,
    List<string> Variants);

/// <summary>
/// Extracts and groups distinct proteins from a collection of recipes.
/// </summary>
public class ProteinAggregator
{
    // Maps raw protein keywords to a canonical root name
    private static readonly Dictionary<string, string> CanonicalMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["chicken"] = "Chicken", ["thigh"] = "Chicken", ["breast"] = "Chicken",
        ["beef"] = "Beef", ["steak"] = "Beef", ["sirloin"] = "Beef",
        ["ribeye"] = "Beef", ["filet"] = "Beef", ["ground meat"] = "Beef",
        ["bison"] = "Beef",
        ["pork"] = "Pork", ["tenderloin"] = "Pork", ["chorizo"] = "Pork",
        ["ham"] = "Pork", ["bacon"] = "Pork",
        ["turkey"] = "Turkey",
        ["lamb"] = "Lamb", ["veal"] = "Veal", ["duck"] = "Duck",
        ["salmon"] = "Salmon", ["cod"] = "Cod", ["tilapia"] = "Tilapia",
        ["tuna"] = "Tuna", ["mahi"] = "Mahi Mahi", ["halibut"] = "Halibut",
        ["trout"] = "Trout", ["catfish"] = "Catfish",
        ["shrimp"] = "Shrimp", ["prawn"] = "Shrimp",
        ["scallop"] = "Scallops", ["crab"] = "Crab", ["lobster"] = "Lobster",
        ["mussel"] = "Mussels", ["clam"] = "Clams",
        ["tofu"] = "Tofu", ["tempeh"] = "Tempeh", ["seitan"] = "Seitan",
        ["beyond"] = "Plant-Based", ["impossible"] = "Plant-Based",
        ["sausage"] = "Sausage", ["pepperoni"] = "Sausage", ["meatball"] = "Meatball",
        ["egg"] = "Eggs",
        ["lentil"] = "Lentils", ["chickpea"] = "Chickpeas",
        ["black bean"] = "Black Beans", ["kidney bean"] = "Kidney Beans",
        ["white bean"] = "White Beans", ["cannellini"] = "White Beans",
        ["edamame"] = "Edamame",
        ["anchov"] = "Anchovies"
    };

    public IReadOnlyList<ProteinSummary> GetAvailableProteins(IEnumerable<RecipeData> recipes)
    {
        // canonical name -> (recipe count, set of variant names)
        var groups = new Dictionary<string, (HashSet<RecipeData> Recipes, HashSet<string> Variants)>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var recipe in recipes)
        {
            foreach (var ingredient in recipe.Ingredients)
            {
                if (IngredientClassifier.Classify(ingredient) != IngredientCategory.Protein)
                    continue;

                var canonical = ResolveCanonical(ingredient.Name ?? ingredient.DisplayText ?? "");
                if (canonical is null)
                    continue;

                if (!groups.TryGetValue(canonical, out var group))
                {
                    group = (new HashSet<RecipeData>(), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                    groups[canonical] = group;
                }

                group.Recipes.Add(recipe);
                var variantName = (ingredient.Name ?? ingredient.DisplayText ?? "").Trim();
                if (!string.IsNullOrEmpty(variantName))
                    group.Variants.Add(variantName);
            }
        }

        return groups
            .Select(g => new ProteinSummary(
                g.Key,
                g.Value.Recipes.Count,
                g.Value.Variants.OrderBy(v => v).ToList()))
            .OrderByDescending(p => p.RecipeCount)
            .ToList();
    }

    private static string? ResolveCanonical(string ingredientName)
    {
        var lower = ingredientName.ToLowerInvariant();

        // Check longer keys first for specificity
        foreach (var (keyword, canonical) in CanonicalMap.OrderByDescending(kv => kv.Key.Length))
        {
            if (lower.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                return canonical;
        }

        return null;
    }
}
