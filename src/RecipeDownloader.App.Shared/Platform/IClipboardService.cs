namespace RecipeDownloader.App.Platform;

/// <summary>
/// Writes text to the host's clipboard.
/// </summary>
public interface IClipboardService
{
    Task SetTextAsync(string text);
}
