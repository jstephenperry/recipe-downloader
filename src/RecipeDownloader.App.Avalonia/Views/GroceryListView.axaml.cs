using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RecipeDownloader.App.Avalonia.Views;

public partial class GroceryListView : UserControl
{
    public GroceryListView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
