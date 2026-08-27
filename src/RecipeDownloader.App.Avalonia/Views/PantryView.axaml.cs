using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RecipeDownloader.App.Avalonia.Views;

public partial class PantryView : UserControl
{
    public PantryView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
