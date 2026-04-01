using RecipeDownloader.Core.Classification;

namespace RecipeDownloader.Core.GroceryList;

public class GroceryItem
{
    public string Name { get; set; } = "";
    public string? CombinedQuantity { get; set; }
    public string? Unit { get; set; }
    public IngredientCategory Category { get; set; }
    public bool IsShared { get; set; }
    public List<string> UsedInRecipes { get; set; } = [];
}
