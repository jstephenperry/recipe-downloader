using System.Windows;
using RecipeDownloader.ViewModels.Services;

namespace RecipeDownloader.App.Services;

public class WpfClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        try
        {
            // SetDataObject(copy: false) tolerates the clipboard being briefly
            // locked by another process (CLIPBRD_E_CANT_OPEN), unlike SetText.
            Clipboard.SetDataObject(text, false);
        }
        catch
        {
            // Clipboard locked by another process — non-critical
        }
    }
}
