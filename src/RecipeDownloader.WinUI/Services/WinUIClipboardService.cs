using RecipeDownloader.ViewModels.Services;
using Windows.ApplicationModel.DataTransfer;

namespace RecipeDownloader.WinUI.Services;

public class WinUIClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        try
        {
            var package = new DataPackage();
            package.SetText(text);
            Clipboard.SetContent(package);
        }
        catch
        {
            // Clipboard locked by another process — non-critical
        }
    }
}
