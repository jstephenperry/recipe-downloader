using System.Text.Json;

namespace RecipeDownloader.Core.Storage;

public class RecipeCatalogStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly string _baseDirectory;

    public RecipeCatalogStore(string baseDirectory)
    {
        _baseDirectory = baseDirectory;
        Directory.CreateDirectory(_baseDirectory);
    }

    public async Task<RecipeCatalog?> LoadAsync(string providerName, CancellationToken ct = default)
    {
        var path = GetFilePath(providerName);
        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<RecipeCatalog>(stream, JsonOptions, ct);
    }

    public async Task SaveAsync(RecipeCatalog catalog, CancellationToken ct = default)
    {
        var path = GetFilePath(catalog.ProviderName);
        var tempPath = path + ".tmp";
        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, catalog, JsonOptions, ct);
        }
        File.Move(tempPath, path, overwrite: true);
    }

    private string GetFilePath(string providerName)
        => Path.Combine(_baseDirectory, $"{providerName}.json");
}
