namespace RecipeDownloader.App.Platform;

/// <summary>
/// The host-specific services the view models need. Each desktop host (WPF, Avalonia)
/// supplies its own implementations at startup, which is what lets the view models
/// themselves stay free of any platform-specific reference.
/// </summary>
/// <param name="FolderPicker">Native folder-selection dialog.</param>
/// <param name="Clipboard">Clipboard access.</param>
/// <param name="FileLauncher">Shell integration for opening files and URLs.</param>
public record PlatformServices(
    IFolderPicker FolderPicker,
    IClipboardService Clipboard,
    IFileLauncher FileLauncher);
