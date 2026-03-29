using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

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

    public MainViewModel()
    {
        LoadSettings();
    }

    public void AddProvider(ProviderViewModel provider)
    {
        Providers.Add(provider);
        SelectedProvider ??= provider;
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
