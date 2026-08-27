using Microsoft.Win32;
using RecipeDownloader.App.Platform;

namespace RecipeDownloader.App.Wpf.Platform;

public class WpfFolderPicker : IFolderPicker
{
    public Task<string?> PickFolderAsync(string title, string? initialDirectory)
    {
        var dialog = new OpenFolderDialog
        {
            Title = title,
            InitialDirectory = initialDirectory
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FolderName : null);
    }
}
