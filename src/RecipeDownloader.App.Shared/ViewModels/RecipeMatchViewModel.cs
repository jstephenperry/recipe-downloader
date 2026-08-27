using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeDownloader.Core.Matching;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.App.ViewModels;

public partial class RecipeMatchViewModel : ObservableObject
{
    private readonly RecipeMatcher _matcher;
    private readonly List<RecipeData> _allRecipeData;
    private readonly PantryInventory _pantry;
    private readonly Action<RecipePairMatch> _onPairSelected;

    public ObservableCollection<RecipePairMatchViewModel> Matches { get; } = [];

    [ObservableProperty]
    private RecipePairMatchViewModel? _selectedMatch;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _protein1 = "";

    [ObservableProperty]
    private string _protein2 = "";

    public RecipeMatchViewModel(
        List<RecipeData> allRecipeData,
        PantryInventory pantry,
        Action<RecipePairMatch> onPairSelected)
    {
        _matcher = new RecipeMatcher();
        _allRecipeData = allRecipeData;
        _pantry = pantry;
        _onPairSelected = onPairSelected;
    }

    public void FindMatches(string protein1, string protein2)
    {
        Protein1 = protein1;
        Protein2 = protein2;

        var results = _matcher.FindBestPairs(_allRecipeData, protein1, protein2, _pantry);

        Matches.Clear();
        foreach (var match in results)
            Matches.Add(new RecipePairMatchViewModel(match));

        StatusText = results.Count > 0
            ? $"Found {results.Count} recipe pairs for {protein1} + {protein2}. Select a pair to generate a grocery list."
            : $"No overlapping recipe pairs found for {protein1} + {protein2}.";
    }

    [RelayCommand]
    private void SelectPair()
    {
        if (SelectedMatch is null)
        {
            StatusText = "Please select a recipe pair.";
            return;
        }

        _onPairSelected(SelectedMatch.Match);
    }
}

public partial class RecipePairMatchViewModel : ObservableObject
{
    public RecipePairMatch Match { get; }

    public string Recipe1Title => Match.Recipe1.Title;
    public string Recipe2Title => Match.Recipe2.Title;
    public int SharedCount => Match.SharedIngredients.Count;
    public string SharedDisplay => string.Join(", ", Match.SharedIngredients);
    public string UniqueToR1Display => string.Join(", ", Match.UniqueToRecipe1);
    public string UniqueToR2Display => string.Join(", ", Match.UniqueToRecipe2);
    public string ScoreDisplay => $"{Match.OverlapScore:P0}";

    public RecipePairMatchViewModel(RecipePairMatch match)
    {
        Match = match;
    }
}
