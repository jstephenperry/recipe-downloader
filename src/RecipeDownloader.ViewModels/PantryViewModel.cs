using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeDownloader.Core.Classification;
using RecipeDownloader.Core.Models;
using RecipeDownloader.Core.Storage;

namespace RecipeDownloader.ViewModels;

public partial class PantryViewModel : ObservableObject
{
    private readonly PantryStore _store;
    private PantryInventory _inventory = new();

    public ObservableCollection<PantryItemViewModel> Items { get; } = [];

    [ObservableProperty]
    private string _newItemName = "";

    [ObservableProperty]
    private string _newItemQuantity = "";

    [ObservableProperty]
    private string _newItemUnit = "";

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private int _itemCount;

    public PantryInventory Inventory => _inventory;

    public PantryViewModel(PantryStore store)
    {
        _store = store;
    }

    public async Task LoadAsync()
    {
        _inventory = await _store.LoadAsync();
        RefreshItems();
        StatusText = $"{Items.Count} items in pantry.";
    }

    [RelayCommand]
    private async Task AddItem()
    {
        if (string.IsNullOrWhiteSpace(NewItemName))
        {
            StatusText = "Enter an ingredient name.";
            return;
        }

        var normalized = IngredientNormalizer.Normalize(NewItemName);
        if (_inventory.Contains(normalized))
        {
            StatusText = $"\"{NewItemName}\" is already in your pantry.";
            return;
        }

        var item = new PantryItem
        {
            Name = normalized,
            DisplayName = NewItemName.Trim(),
            Quantity = string.IsNullOrWhiteSpace(NewItemQuantity) ? null : NewItemQuantity.Trim(),
            Unit = string.IsNullOrWhiteSpace(NewItemUnit) ? null : NewItemUnit.Trim(),
            AddedAt = DateTimeOffset.Now
        };

        _inventory.Items.Add(item);
        await _store.SaveAsync(_inventory);

        RefreshItems();
        NewItemName = "";
        NewItemQuantity = "";
        NewItemUnit = "";
        StatusText = $"Added \"{item.DisplayName}\" to pantry.";
    }

    [RelayCommand]
    private async Task RemoveItem(PantryItemViewModel item)
    {
        var toRemove = _inventory.Items.FirstOrDefault(i =>
            i.Name.Equals(item.NormalizedName, StringComparison.OrdinalIgnoreCase));

        if (toRemove is not null)
        {
            _inventory.Items.Remove(toRemove);
            await _store.SaveAsync(_inventory);
            RefreshItems();
            StatusText = $"Removed \"{item.DisplayName}\" from pantry.";
        }
    }

    [RelayCommand]
    private async Task ClearAll()
    {
        _inventory.Items.Clear();
        await _store.SaveAsync(_inventory);
        RefreshItems();
        StatusText = "Pantry cleared.";
    }

    private void RefreshItems()
    {
        Items.Clear();
        foreach (var item in _inventory.Items.OrderBy(i => i.DisplayName ?? i.Name))
            Items.Add(new PantryItemViewModel(item));
        ItemCount = Items.Count;
    }
}

public partial class PantryItemViewModel : ObservableObject
{
    public string NormalizedName { get; }
    public string DisplayName { get; }
    public string QuantityDisplay { get; }

    public PantryItemViewModel(PantryItem item)
    {
        NormalizedName = item.Name;
        DisplayName = item.DisplayName ?? item.Name;

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(item.Quantity))
            parts.Add(item.Quantity);
        if (!string.IsNullOrEmpty(item.Unit))
            parts.Add(item.Unit);
        QuantityDisplay = parts.Count > 0 ? string.Join(" ", parts) : "";
    }
}
