using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Storage;

public class RecipeCatalog
{
    public string ProviderName { get; set; } = "";
    public DateTimeOffset LastRefreshed { get; set; }
    public List<Recipe> Recipes { get; set; } = [];
}
