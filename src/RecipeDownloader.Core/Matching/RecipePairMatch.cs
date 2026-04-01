using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Matching;

public record RecipePairMatch(
    RecipeData Recipe1,
    RecipeData Recipe2,
    IReadOnlyList<string> SharedIngredients,
    IReadOnlyList<string> UniqueToRecipe1,
    IReadOnlyList<string> UniqueToRecipe2,
    double OverlapScore);
