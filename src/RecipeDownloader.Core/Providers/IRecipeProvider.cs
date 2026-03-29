using RecipeDownloader.Core.Models;

namespace RecipeDownloader.Core.Providers;

public interface IRecipeProvider
{
    string Name { get; }

    /// <summary>
    /// The file extension this provider produces when downloading (e.g. ".pdf", ".html").
    /// </summary>
    string DownloadFileExtension { get; }

    Task<IReadOnlyList<Recipe>> DiscoverRecipesAsync(
        IProgress<DiscoveryProgress>? progress = null,
        CancellationToken ct = default);

    /// <summary>
    /// Downloads a recipe to the output directory. Returns the path of the primary file saved.
    /// For PDF providers this downloads the PDF. For data-driven providers this saves JSON + HTML.
    /// </summary>
    Task<string> DownloadRecipeAsync(
        Recipe recipe,
        string outputDirectory,
        CancellationToken ct = default);
}
