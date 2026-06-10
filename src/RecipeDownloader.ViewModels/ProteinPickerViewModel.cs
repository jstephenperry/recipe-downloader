using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeDownloader.Core.Classification;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.ViewModels;

public partial class ProteinPickerViewModel : ObservableObject
{
    private readonly Action<string, string> _onProteinsSelected;
    private readonly List<RecipeData> _allRecipeData;

    public ObservableCollection<ProteinOptionViewModel> Proteins { get; } = [];

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private bool _isLoaded;

    public ProteinPickerViewModel(
        List<RecipeData> allRecipeData,
        Action<string, string> onProteinsSelected)
    {
        _allRecipeData = allRecipeData;
        _onProteinsSelected = onProteinsSelected;
    }

    [RelayCommand]
    private void LoadProteins()
    {
        var aggregator = new ProteinAggregator();
        var proteins = aggregator.GetAvailableProteins(_allRecipeData);

        Proteins.Clear();
        foreach (var p in proteins)
        {
            var vm = new ProteinOptionViewModel(p);
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ProteinOptionViewModel.IsSelected))
                    EnforceMaxSelection(vm);
            };
            Proteins.Add(vm);
        }

        StatusText = $"Found {proteins.Count} proteins across {_allRecipeData.Count} recipes. Select up to 2.";
        IsLoaded = true;
    }

    [RelayCommand]
    private void FindMatchingRecipes()
    {
        var selected = Proteins.Where(p => p.IsSelected).ToList();
        if (selected.Count != 2)
        {
            StatusText = "Please select exactly 2 proteins.";
            return;
        }

        _onProteinsSelected(selected[0].CanonicalName, selected[1].CanonicalName);
    }

    private void EnforceMaxSelection(ProteinOptionViewModel changed)
    {
        var selectedCount = Proteins.Count(p => p.IsSelected);
        if (selectedCount > 2 && changed.IsSelected)
        {
            changed.IsSelected = false;
            StatusText = "Maximum 2 proteins can be selected.";
            return;
        }

        StatusText = selectedCount switch
        {
            0 => "Select up to 2 proteins.",
            1 => "Select 1 more protein.",
            2 => "Ready! Click 'Find Matching Recipes' to continue.",
            _ => StatusText
        };

        FindMatchingRecipesCommand.NotifyCanExecuteChanged();
    }
}

public partial class ProteinOptionViewModel : ObservableObject
{
    public string CanonicalName { get; }
    public int RecipeCount { get; }
    public string VariantsDisplay { get; }

    [ObservableProperty]
    private bool _isSelected;

    public ProteinOptionViewModel(ProteinSummary summary)
    {
        CanonicalName = summary.CanonicalName;
        RecipeCount = summary.RecipeCount;
        VariantsDisplay = string.Join(", ", summary.Variants.Take(5));
        if (summary.Variants.Count > 5)
            VariantsDisplay += $" (+{summary.Variants.Count - 5} more)";
    }
}
