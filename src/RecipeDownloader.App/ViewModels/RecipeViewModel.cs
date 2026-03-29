using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RecipeDownloader.Core.Models;

namespace RecipeDownloader.App.ViewModels;

public partial class RecipeViewModel : ObservableObject
{
    public Recipe Recipe { get; }

    public string Name => Recipe.Name;
    public string SourceUrl => Recipe.SourceUrl;
    public string? PdfUrl => Recipe.PdfUrl;
    public bool HasPdf => !string.IsNullOrEmpty(Recipe.PdfUrl);

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private DownloadStatus _status;

    [ObservableProperty]
    private string? _localFilePath;

    [ObservableProperty]
    private string? _localJsonPath;

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasLocalFile => !string.IsNullOrEmpty(LocalFilePath) && File.Exists(LocalFilePath);
    public bool HasLocalJson => !string.IsNullOrEmpty(LocalJsonPath) && File.Exists(LocalJsonPath);

    public RecipeViewModel(Recipe recipe)
    {
        Recipe = recipe;
        _status = HasPdf ? DownloadStatus.NotDownloaded : DownloadStatus.NoPdf;
    }

    partial void OnLocalFilePathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasLocalFile));
        // Auto-detect companion JSON file
        if (value is not null)
        {
            var jsonPath = Path.ChangeExtension(value, ".json");
            if (File.Exists(jsonPath))
                LocalJsonPath = jsonPath;
        }
    }

    partial void OnLocalJsonPathChanged(string? value)
    {
        OnPropertyChanged(nameof(HasLocalJson));
    }

    [RelayCommand]
    private void OpenInBrowser()
    {
        Process.Start(new ProcessStartInfo(SourceUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenPdfInBrowser()
    {
        if (PdfUrl is not null)
            Process.Start(new ProcessStartInfo(PdfUrl) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenLocalFile()
    {
        if (LocalFilePath is not null && File.Exists(LocalFilePath))
            Process.Start(new ProcessStartInfo(LocalFilePath) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenLocalJson()
    {
        if (LocalJsonPath is not null && File.Exists(LocalJsonPath))
            Process.Start(new ProcessStartInfo(LocalJsonPath) { UseShellExecute = true });
    }
}
