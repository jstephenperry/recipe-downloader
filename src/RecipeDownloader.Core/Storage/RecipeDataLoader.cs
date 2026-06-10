using System.Text.Json;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Storage;

/// <summary>
/// Loads RecipeData JSON files produced by recipe downloads, for meal planning.
/// </summary>
public static class RecipeDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static async Task<List<RecipeData>> LoadAllAsync(string outputDir, CancellationToken ct = default)
    {
        var recipes = new List<RecipeData>();

        if (!Directory.Exists(outputDir))
            return recipes;

        foreach (var jsonFile in Directory.EnumerateFiles(outputDir, "*.json", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                // Skip catalog/settings files
                var fileName = Path.GetFileName(jsonFile);
                if (fileName is "settings.json" or "pantry.json" ||
                    fileName.EndsWith("Catalog.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                await using var stream = File.OpenRead(jsonFile);
                var data = await JsonSerializer.DeserializeAsync<RecipeData>(stream, JsonOptions, ct);
                if (data is not null && !string.IsNullOrEmpty(data.Title) && data.Ingredients.Count > 0)
                    recipes.Add(data);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Skip files that aren't valid RecipeData JSON
            }
        }

        return recipes;
    }
}
