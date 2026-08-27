using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RecipeDownloader.App.Avalonia.Views;

public partial class RecipeDetailView : UserControl
{
    public RecipeDetailView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
