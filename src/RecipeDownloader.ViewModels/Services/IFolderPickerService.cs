namespace RecipeDownloader.ViewModels.Services;

/// <summary>
/// Platform abstraction for folder selection dialogs.
/// Implemented per UI framework (WPF OpenFolderDialog, WinUI FolderPicker).
/// </summary>
public interface IFolderPickerService
{
    /// <summary>Shows a folder picker. Returns the selected path, or null if cancelled.</summary>
    Task<string?> PickFolderAsync(string? initialDirectory = null);
}
