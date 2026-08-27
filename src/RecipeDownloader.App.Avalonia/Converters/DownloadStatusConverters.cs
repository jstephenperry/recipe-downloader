using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using RecipeDownloader.App.ViewModels;

namespace RecipeDownloader.App.Avalonia.Converters;

public class DownloadStatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DownloadStatus status
            ? status switch
            {
                DownloadStatus.NotDownloaded => "Not Downloaded",
                DownloadStatus.Downloading => "Downloading...",
                DownloadStatus.Downloaded => "Downloaded",
                DownloadStatus.Failed => "Failed",
                DownloadStatus.NoPdf => "No PDF",
                _ => ""
            }
            : "";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class DownloadStatusToColorConverter : IValueConverter
{
    private static readonly IBrush Downloaded = new SolidColorBrush(Color.FromRgb(34, 139, 34));
    private static readonly IBrush Downloading = new SolidColorBrush(Color.FromRgb(30, 144, 255));
    private static readonly IBrush Failed = new SolidColorBrush(Color.FromRgb(220, 20, 60));
    private static readonly IBrush NoPdf = new SolidColorBrush(Color.FromRgb(169, 169, 169));
    private static readonly IBrush Default = new SolidColorBrush(Color.FromRgb(100, 100, 100));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DownloadStatus status
            ? status switch
            {
                DownloadStatus.Downloaded => Downloaded,
                DownloadStatus.Downloading => Downloading,
                DownloadStatus.Failed => Failed,
                DownloadStatus.NoPdf => NoPdf,
                _ => Default
            }
            : Default;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
