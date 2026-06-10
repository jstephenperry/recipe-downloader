namespace RecipeDownloader.ViewModels.Services;

/// <summary>
/// Platform abstraction for clipboard access.
/// Implemented per UI framework (WPF Clipboard, WinUI DataPackage).
/// </summary>
public interface IClipboardService
{
    void SetText(string text);
}
