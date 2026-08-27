using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeDownloader.Core.Models;
using RecipeDownloader.Core.Providers;
using RecipeDownloader.Core.Storage;
using RecipeDownloader.App.Platform;

namespace RecipeDownloader.App.ViewModels;

public partial class ProviderViewModel : ObservableObject
{
    private readonly IRecipeProvider _provider;
    private readonly RecipeCatalogStore _catalogStore;
    private readonly Func<string> _getOutputDirectory;
    private CancellationTokenSource? _discoveryCts;
    private CancellationTokenSource? _downloadCts;

    public string Name => _provider.Name;
    public ObservableCollection<RecipeViewModel> Recipes { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DiscoverCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(DownloadAllCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private double _progressMax = 1;

    [ObservableProperty]
    private bool _isProgressIndeterminate;

    [ObservableProperty]
    private RecipeViewModel? _selectedRecipe;

    [ObservableProperty]
    private DateTimeOffset? _lastRefreshed;

    private readonly IFileLauncher _launcher;

    public ProviderViewModel(
        IRecipeProvider provider,
        RecipeCatalogStore catalogStore,
        Func<string> getOutputDirectory,
        IFileLauncher launcher)
    {
        _provider = provider;
        _catalogStore = catalogStore;
        _getOutputDirectory = getOutputDirectory;
        _launcher = launcher;
    }

    public async Task LoadCatalogAsync()
    {
        var catalog = await _catalogStore.LoadAsync(_provider.Name);
        if (catalog is null)
            return;

        LastRefreshed = catalog.LastRefreshed;
        Recipes.Clear();
        foreach (var recipe in catalog.Recipes.OrderBy(r => r.Name))
            Recipes.Add(new RecipeViewModel(recipe, _launcher));

        StatusText = $"Loaded {Recipes.Count} recipes from cache (last refreshed: {LastRefreshed:g})";
        CheckExistingDownloads();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteWhenNotBusy))]
    private async Task DiscoverAsync()
    {
        IsBusy = true;
        IsProgressIndeterminate = true;
        StatusText = "Discovering recipes...";
        _discoveryCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<DiscoveryProgress>(p =>
            {
                StatusText = $"{p.Phase}: {p.Detail ?? ""}";
                if (p.Total > 0)
                {
                    IsProgressIndeterminate = false;
                    ProgressMax = p.Total;
                    ProgressValue = p.Current;
                }
            });

            var recipes = await _provider.DiscoverRecipesAsync(progress, _discoveryCts.Token);

            var catalog = new RecipeCatalog
            {
                ProviderName = _provider.Name,
                LastRefreshed = DateTimeOffset.Now,
                Recipes = recipes.ToList()
            };
            await _catalogStore.SaveAsync(catalog, _discoveryCts.Token);

            LastRefreshed = catalog.LastRefreshed;
            Recipes.Clear();
            foreach (var recipe in recipes.OrderBy(r => r.Name))
                Recipes.Add(new RecipeViewModel(recipe, _launcher));

            StatusText = $"Discovered {recipes.Count} recipes";
            CheckExistingDownloads();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Discovery cancelled";
        }
        catch (Exception ex)
        {
            StatusText = $"Discovery failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsProgressIndeterminate = false;
            _discoveryCts?.Dispose();
            _discoveryCts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanExecuteWhenNotBusy))]
    private async Task DownloadSelectedAsync()
    {
        var selected = Recipes.Where(r => r.IsSelected && r.HasPdf && r.Status != DownloadStatus.Downloaded).ToList();
        if (selected.Count == 0)
        {
            StatusText = "No downloadable recipes selected";
            return;
        }
        await DownloadRecipesAsync(selected);
    }

    [RelayCommand(CanExecute = nameof(CanExecuteWhenNotBusy))]
    private async Task DownloadAllAsync()
    {
        var downloadable = Recipes.Where(r => r.HasPdf && r.Status != DownloadStatus.Downloaded).ToList();
        if (downloadable.Count == 0)
        {
            StatusText = "No recipes to download";
            return;
        }
        await DownloadRecipesAsync(downloadable);
    }

    [RelayCommand]
    private void CancelOperation()
    {
        _discoveryCts?.Cancel();
        _downloadCts?.Cancel();
    }

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var r in Recipes) r.IsSelected = true;
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var r in Recipes) r.IsSelected = false;
    }

    private bool CanExecuteWhenNotBusy() => !IsBusy;

    private const int MaxDownloadConcurrency = 6;

    private async Task DownloadRecipesAsync(List<RecipeViewModel> recipes)
    {
        IsBusy = true;
        ProgressMax = recipes.Count;
        ProgressValue = 0;
        _downloadCts = new CancellationTokenSource();

        var outputDir = Path.Combine(_getOutputDirectory(), _provider.Name);
        var succeeded = 0;
        var failed = 0;
        var completed = 0;
        var semaphore = new SemaphoreSlim(MaxDownloadConcurrency);

        try
        {
            var tasks = recipes.Select(async vm =>
            {
                await semaphore.WaitAsync(_downloadCts.Token);
                try
                {
                    _downloadCts.Token.ThrowIfCancellationRequested();

                    // UI updates must happen on the UI thread — dispatch via the captured SynchronizationContext
                    vm.Status = DownloadStatus.Downloading;

                    var path = await _provider.DownloadRecipeAsync(vm.Recipe, outputDir, _downloadCts.Token);
                    vm.LocalFilePath = path;
                    vm.Status = DownloadStatus.Downloaded;
                    Interlocked.Increment(ref succeeded);
                }
                catch (OperationCanceledException)
                {
                    vm.Status = DownloadStatus.NotDownloaded;
                    throw;
                }
                catch (Exception ex)
                {
                    vm.Status = DownloadStatus.Failed;
                    vm.ErrorMessage = ex.Message;
                    Interlocked.Increment(ref failed);
                }
                finally
                {
                    var current = Interlocked.Increment(ref completed);
                    ProgressValue = current;
                    StatusText = $"Downloading [{current}/{recipes.Count}]...";
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            StatusText = $"Download complete: {succeeded} succeeded, {failed} failed";
        }
        catch (OperationCanceledException)
        {
            StatusText = $"Download cancelled ({succeeded} completed, {failed} failed)";
        }
        finally
        {
            IsBusy = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    private void CheckExistingDownloads()
    {
        var outputDir = Path.Combine(_getOutputDirectory(), _provider.Name);
        if (!Directory.Exists(outputDir))
            return;

        var ext = _provider.DownloadFileExtension;
        var pattern = $"*{ext}";
        var existingFiles = new HashSet<string>(
            Directory.GetFiles(outputDir, pattern).Select(Path.GetFileName)!,
            StringComparer.OrdinalIgnoreCase);

        var existingJsonFiles = new HashSet<string>(
            Directory.GetFiles(outputDir, "*.json").Select(Path.GetFileName)!,
            StringComparer.OrdinalIgnoreCase);

        foreach (var vm in Recipes)
        {
            if (!vm.HasPdf) continue;

            var baseName = SanitizeFileName(vm.Name);
            var expectedName = baseName + ext;
            if (existingFiles.Contains(expectedName))
            {
                vm.LocalFilePath = Path.Combine(outputDir, expectedName);
                vm.Status = DownloadStatus.Downloaded;
            }

            // Also detect companion JSON data files
            var jsonName = baseName + ".json";
            if (existingJsonFiles.Contains(jsonName))
            {
                vm.LocalJsonPath = Path.Combine(outputDir, jsonName);
            }
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("", name.Select(c => invalid.Contains(c) ? '_' : c));
        if (sanitized.Length > 200)
            sanitized = sanitized[..200];
        return sanitized;
    }
}
