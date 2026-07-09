using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using RecipeDownloader.Core.Matching;
using RecipeDownloader.Core.Models;
using RecipeDownloader.Core.Storage;

namespace RecipeDownloader.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly string SettingsDirectory =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RecipeDownloader");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    public ObservableCollection<ProviderViewModel> Providers { get; } = [];

    [ObservableProperty]
    private ProviderViewModel? _selectedProvider;

    [ObservableProperty]
    private string _outputDirectory = "";

    // Navigation state
    [ObservableProperty]
    private string _activeView = "Recipes";

    [ObservableProperty]
    private PantryViewModel? _pantryViewModel;

    [ObservableProperty]
    private ProteinPickerViewModel? _proteinPickerViewModel;

    [ObservableProperty]
    private RecipeMatchViewModel? _recipeMatchViewModel;

    [ObservableProperty]
    private GroceryListViewModel? _groceryListViewModel;

    private PantryStore? _pantryStore;
    private List<RecipeData> _allRecipeData = [];

    public MainViewModel()
    {
        LoadSettings();
    }

    public void AddProvider(ProviderViewModel provider)
    {
        Providers.Add(provider);
        SelectedProvider ??= provider;
    }

    /// <summary>
    /// Runs discovery for every provider concurrently. Each provider already
    /// parallelizes its own network work internally, so the whole catalog
    /// refreshes in the time of the slowest single provider rather than the sum.
    /// </summary>
    [RelayCommand]
    private async Task DiscoverAllAsync()
    {
        var tasks = Providers
            .Where(p => p.DiscoverCommand.CanExecute(null))
            .Select(p => p.DiscoverCommand.ExecuteAsync(null))
            .ToList();

        if (tasks.Count == 0)
            return;

        await Task.WhenAll(tasks);
    }

    public void SetPantryStore(PantryStore store)
    {
        _pantryStore = store;
        PantryViewModel = new PantryViewModel(store);
    }

    public void SetRecipeData(List<RecipeData> recipeData)
    {
        _allRecipeData = recipeData;
    }

    [RelayCommand]
    private void NavigateTo(string viewName)
    {
        ActiveView = viewName;

        if (viewName == "MealPlanner" && ProteinPickerViewModel is null)
        {
            ProteinPickerViewModel = new ProteinPickerViewModel(
                _allRecipeData,
                OnProteinsSelected);
            ProteinPickerViewModel.LoadProteinsCommand.Execute(null);
        }
    }

    private void OnProteinsSelected(string protein1, string protein2)
    {
        var pantry = PantryViewModel?.Inventory ?? new PantryInventory();
        RecipeMatchViewModel = new RecipeMatchViewModel(
            _allRecipeData, pantry, OnPairSelected);
        RecipeMatchViewModel.FindMatches(protein1, protein2);
        ActiveView = "RecipeMatch";
    }

    private void OnPairSelected(RecipePairMatch pair)
    {
        var pantry = PantryViewModel?.Inventory ?? new PantryInventory();
        GroceryListViewModel = new GroceryListViewModel();
        GroceryListViewModel.Generate(pair, pantry);
        ActiveView = "GroceryList";
    }

    [RelayCommand]
    private void BrowseOutputDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select Recipe Output Directory",
            InitialDirectory = Directory.Exists(OutputDirectory) ? OutputDirectory : null
        };

        if (dialog.ShowDialog() == true)
        {
            OutputDirectory = dialog.FolderName;
            SaveSettings();
        }
    }

    public string GetOutputDirectory()
    {
        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            OutputDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Recipes");
        }

        Directory.CreateDirectory(OutputDirectory);
        return OutputDirectory;
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                OutputDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Recipes");
                return;
            }

            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            OutputDirectory = settings?.OutputDirectory
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Recipes");
        }
        catch
        {
            OutputDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Recipes");
        }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            var json = JsonSerializer.Serialize(new AppSettings { OutputDirectory = OutputDirectory },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Settings save failure is non-critical
        }
    }

    private class AppSettings
    {
        public string OutputDirectory { get; set; } = "";
    }
}
