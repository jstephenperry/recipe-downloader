using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace RecipeDownloader.App.Avalonia.Views;

public partial class ProteinPickerView : UserControl
{
    public ProteinPickerView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
