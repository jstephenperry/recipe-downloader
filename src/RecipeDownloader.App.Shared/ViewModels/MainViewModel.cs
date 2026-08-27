using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeDownloader.App.Platform;
using RecipeDownloader.Core.Matching;
using RecipeDownloader.Core.Models;
using RecipeDownloader.Core.Storage;

namespace RecipeDownloader.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
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

    private readonly PlatformServices _platform;
    private PantryStore? _pantryStore;
    private List<RecipeData> _allRecipeData = [];

    public MainViewModel(PlatformServices platform)
    {
        _platform = platform;
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
        GroceryListViewModel = new GroceryListViewModel(_platform.Clipboard);
        GroceryListViewModel.Generate(pair, pantry);
        ActiveView = "GroceryList";
    }

    [RelayCommand]
    private async Task BrowseOutputDirectoryAsync()
    {
        var selected = await _platform.FolderPicker.PickFolderAsync(
            "Select Recipe Output Directory",
            Directory.Exists(OutputDirectory) ? OutputDirectory : null);

        if (string.IsNullOrWhiteSpace(selected))
            return;

        OutputDirectory = selected;
        SaveSettings();
    }

    public string GetOutputDirectory()
    {
        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            OutputDirectory = AppPaths.DefaultOutputDirectory;
        }

        Directory.CreateDirectory(OutputDirectory);
        return OutputDirectory;
    }

    private void LoadSettings()
    {
        try
        {
            if (!File.Exists(AppPaths.SettingsPath))
            {
                OutputDirectory = AppPaths.DefaultOutputDirectory;
                return;
            }

            var json = File.ReadAllText(AppPaths.SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json);
            OutputDirectory = settings?.OutputDirectory ?? AppPaths.DefaultOutputDirectory;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            OutputDirectory = AppPaths.DefaultOutputDirectory;
        }
    }

    private void SaveSettings()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            var json = JsonSerializer.Serialize(new AppSettings { OutputDirectory = OutputDirectory },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppPaths.SettingsPath, json);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings save failure is non-critical
        }
    }

    private class AppSettings
    {
        public string OutputDirectory { get; set; } = "";
    }
}
