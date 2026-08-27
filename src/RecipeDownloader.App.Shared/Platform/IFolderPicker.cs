namespace RecipeDownloader.App.Platform;

/// <summary>
/// Shows the host's native folder-selection dialog.
/// </summary>
public interface IFolderPicker
{
    /// <summary>
    /// Prompts the user to choose a folder. Returns null when the user cancels.
    /// </summary>
    Task<string?> PickFolderAsync(string title, string? initialDirectory);
}
