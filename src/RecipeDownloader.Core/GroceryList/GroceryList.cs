namespace RecipeDownloader.Core.GroceryList;

public class GroceryList
{
    public List<GroceryItem> Items { get; set; } = [];
    public List<GroceryItem> AlreadyOnHand { get; set; } = [];
    public int TotalUniqueIngredients { get; set; }
    public int IngredientsFromPantry { get; set; }
    public int SharedBetweenRecipes { get; set; }
    public double WasteEfficiency { get; set; }
}
