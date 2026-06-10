using System.Text.Json;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Storage;

public class PantryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _filePath;

    public PantryStore(string baseDirectory)
    {
        Directory.CreateDirectory(baseDirectory);
        _filePath = Path.Combine(baseDirectory, "pantry.json");
    }

    public async Task<PantryInventory> LoadAsync(CancellationToken ct = default)
    {
        if (!File.Exists(_filePath))
            return new PantryInventory();

        await using var stream = File.OpenRead(_filePath);
        return await JsonSerializer.DeserializeAsync<PantryInventory>(stream, JsonOptions, ct)
               ?? new PantryInventory();
    }

    public async Task SaveAsync(PantryInventory inventory, CancellationToken ct = default)
    {
        inventory.LastUpdated = DateTimeOffset.Now;
        var tempPath = _filePath + ".tmp";
        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, inventory, JsonOptions, ct);
        }
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
