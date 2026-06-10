using RecipeDownloader.ViewModels.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace RecipeDownloader.WinUI.Services;

public class WinUIFolderPickerService : IFolderPickerService
{
    private readonly Func<IntPtr> _getWindowHandle;

    public WinUIFolderPickerService(Func<IntPtr> getWindowHandle)
    {
        _getWindowHandle = getWindowHandle;
    }

    public async Task<string?> PickFolderAsync(string? initialDirectory = null)
    {
        var picker = new FolderPicker
        {
            SuggestedStartLocation = PickerLocationId.DocumentsLibrary
        };
        picker.FileTypeFilter.Add("*");

        // Pickers in desktop apps must be associated with a window handle
        InitializeWithWindow.Initialize(picker, _getWindowHandle());

        var folder = await picker.PickSingleFolderAsync();
        return folder?.Path;
    }
}
