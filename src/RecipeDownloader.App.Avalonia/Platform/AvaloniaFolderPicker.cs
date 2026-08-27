using Avalonia.Controls;
using Avalonia.Platform.Storage;
using RecipeDownloader.App.Platform;

namespace RecipeDownloader.App.Avalonia.Platform;

/// <summary>
/// Folder picker backed by Avalonia's storage provider, which maps to the native dialog
/// on each platform (Win32 on Windows, NSOpenPanel on macOS, the portal or GTK on Linux).
/// </summary>
public class AvaloniaFolderPicker : IFolderPicker
{
    private readonly Window _owner;

    public AvaloniaFolderPicker(Window owner)
    {
        _owner = owner;
    }

    public async Task<string?> PickFolderAsync(string title, string? initialDirectory)
    {
        var storage = _owner.StorageProvider;
        if (!storage.CanPickFolder)
            return null;

        IStorageFolder? start = null;
        if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            start = await storage.TryGetFolderFromPathAsync(initialDirectory);

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start
        });

        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }
}
