using System.Windows;
using RecipeDownloader.App.Platform;

namespace RecipeDownloader.App.Wpf.Platform;

public class WpfClipboardService : IClipboardService
{
    public Task SetTextAsync(string text)
    {
        Clipboard.SetText(text);
        return Task.CompletedTask;
    }
}
