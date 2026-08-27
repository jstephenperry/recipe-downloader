using Avalonia.Controls;
using Avalonia.Input.Platform;
using RecipeDownloader.App.Platform;

namespace RecipeDownloader.App.Avalonia.Platform;

public class AvaloniaClipboardService : IClipboardService
{
    private readonly Window _owner;

    public AvaloniaClipboardService(Window owner)
    {
        _owner = owner;
    }

    public async Task SetTextAsync(string text)
    {
        var clipboard = _owner.Clipboard;
        if (clipboard is not null)
            await clipboard.SetTextAsync(text);
    }
}
