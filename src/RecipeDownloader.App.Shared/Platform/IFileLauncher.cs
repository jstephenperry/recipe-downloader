namespace RecipeDownloader.App.Platform;

/// <summary>
/// Opens a local file or URL in whatever application the operating system associates with it.
/// </summary>
public interface IFileLauncher
{
    void Open(string pathOrUrl);
}
