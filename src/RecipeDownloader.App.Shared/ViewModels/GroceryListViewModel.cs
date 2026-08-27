using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeDownloader.App.Platform;
using RecipeDownloader.Core.Classification;
using RecipeDownloader.Core.GroceryList;
using RecipeDownloader.Core.Matching;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.App.ViewModels;

public partial class GroceryListViewModel : ObservableObject
{
    private readonly GroceryListGenerator _generator = new();
    private readonly IClipboardService _clipboard;
    private GroceryList? _groceryList;

    public GroceryListViewModel(IClipboardService clipboard)
    {
        _clipboard = clipboard;
    }

    public ObservableCollection<GroceryItemGroupViewModel> Groups { get; } = [];
    public ObservableCollection<GroceryItemViewModel> OnHandItems { get; } = [];

    [ObservableProperty]
    private string _recipe1Title = "";

    [ObservableProperty]
    private string _recipe2Title = "";

    [ObservableProperty]
    private string _summaryText = "";

    [ObservableProperty]
    private bool _hasResults;

    public void Generate(RecipePairMatch pair, PantryInventory pantry)
    {
        _groceryList = _generator.Generate(pair, pantry);
        Recipe1Title = pair.Recipe1.Title;
        Recipe2Title = pair.Recipe2.Title;

        // Group items by category
        Groups.Clear();
        var grouped = _groceryList.Items
            .GroupBy(i => i.Category)
            .OrderBy(g => CategoryOrder(g.Key));

        foreach (var group in grouped)
        {
            Groups.Add(new GroceryItemGroupViewModel(
                CategoryDisplayName(group.Key),
                group.Select(i => new GroceryItemViewModel(i)).ToList()));
        }

        // On-hand items
        OnHandItems.Clear();
        foreach (var item in _groceryList.AlreadyOnHand)
            OnHandItems.Add(new GroceryItemViewModel(item));

        SummaryText = $"{_groceryList.Items.Count} items to buy | " +
                      $"{_groceryList.IngredientsFromPantry} already on hand | " +
                      $"{_groceryList.SharedBetweenRecipes} shared between recipes | " +
                      $"Waste efficiency: {_groceryList.WasteEfficiency:P0}";

        HasResults = true;
    }

    [RelayCommand]
    private async Task CopyToClipboardAsync()
    {
        if (_groceryList is null) return;

        var lines = new List<string>
        {
            $"Grocery List: {Recipe1Title} + {Recipe2Title}",
            new string('=', 50)
        };

        var grouped = _groceryList.Items.GroupBy(i => i.Category).OrderBy(g => CategoryOrder(g.Key));
        foreach (var group in grouped)
        {
            lines.Add("");
            lines.Add($"--- {CategoryDisplayName(group.Key)} ---");
            foreach (var item in group)
            {
                var qty = !string.IsNullOrEmpty(item.CombinedQuantity)
                    ? $"{item.CombinedQuantity} {item.Unit ?? ""}".Trim()
                    : "";
                var shared = item.IsShared ? " [shared]" : "";
                lines.Add($"  [ ] {item.Name}{(qty.Length > 0 ? $" ({qty})" : "")}{shared}");
            }
        }

        if (_groceryList.AlreadyOnHand.Count > 0)
        {
            lines.Add("");
            lines.Add("--- Already On Hand ---");
            foreach (var item in _groceryList.AlreadyOnHand)
                lines.Add($"  [x] {item.Name}");
        }

        lines.Add("");
        lines.Add(SummaryText);

        await _clipboard.SetTextAsync(string.Join(Environment.NewLine, lines));
    }

    private static int CategoryOrder(IngredientCategory cat) => cat switch
    {
        IngredientCategory.Protein => 0,
        IngredientCategory.Vegetable => 1,
        IngredientCategory.Starch => 2,
        IngredientCategory.Dairy => 3,
        IngredientCategory.Seasoning => 4,
        _ => 5
    };

    private static string CategoryDisplayName(IngredientCategory cat) => cat switch
    {
        IngredientCategory.Protein => "Meat & Protein",
        IngredientCategory.Vegetable => "Produce",
        IngredientCategory.Starch => "Grains & Starches",
        IngredientCategory.Dairy => "Dairy",
        IngredientCategory.Seasoning => "Seasonings & Sauces",
        _ => "Other"
    };
}

public class GroceryItemGroupViewModel
{
    public string CategoryName { get; }
    public List<GroceryItemViewModel> Items { get; }
    public int ItemCount => Items.Count;

    public GroceryItemGroupViewModel(string categoryName, List<GroceryItemViewModel> items)
    {
        CategoryName = categoryName;
        Items = items;
    }
}

public partial class GroceryItemViewModel : ObservableObject
{
    public string Name { get; }
    public string QuantityDisplay { get; }
    public bool IsShared { get; }
    public string RecipesDisplay { get; }

    [ObservableProperty]
    private bool _isChecked;

    public GroceryItemViewModel(GroceryItem item)
    {
        Name = item.Name;
        IsShared = item.IsShared;
        RecipesDisplay = string.Join(", ", item.UsedInRecipes);

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(item.CombinedQuantity))
            parts.Add(item.CombinedQuantity);
        if (!string.IsNullOrEmpty(item.Unit))
            parts.Add(item.Unit);
        QuantityDisplay = parts.Count > 0 ? string.Join(" ", parts) : "";
    }
}
