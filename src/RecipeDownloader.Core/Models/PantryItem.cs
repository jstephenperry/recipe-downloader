namespace RecipeDownloader.Core.Models;

public class PantryItem
{
    public string Name { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Quantity { get; set; }
    public string? Unit { get; set; }
    public DateTimeOffset AddedAt { get; set; } = DateTimeOffset.Now;
}

public class PantryInventory
{
    public List<PantryItem> Items { get; set; } = [];
    public DateTimeOffset LastUpdated { get; set; }

    public bool Contains(string normalizedName)
    {
        return Items.Any(i =>
            i.Name.Equals(normalizedName, StringComparison.OrdinalIgnoreCase));
    }
}
