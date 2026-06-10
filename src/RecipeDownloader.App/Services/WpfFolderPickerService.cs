using Microsoft.Win32;
using RecipeDownloader.ViewModels.Services;

namespace RecipeDownloader.App.Services;

public class WpfFolderPickerService : IFolderPickerService
{
    public Task<string?> PickFolderAsync(string? initialDirectory = null)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Recipe Output Directory",
            InitialDirectory = initialDirectory
        };

        return Task.FromResult(dialog.ShowDialog() == true ? dialog.FolderName : null);
    }
}
