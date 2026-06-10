using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using RecipeDownloader.ViewModels;

namespace RecipeDownloader.App.Converters;

public class DownloadStatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is DownloadStatus status ? status switch
        {
            DownloadStatus.NotDownloaded => "Not Downloaded",
            DownloadStatus.Downloading => "Downloading...",
            DownloadStatus.Downloaded => "Downloaded",
            DownloadStatus.Failed => "Failed",
            DownloadStatus.NoPdf => "No PDF",
            _ => ""
        } : "";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class DownloadStatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is DownloadStatus status ? status switch
        {
            DownloadStatus.Downloaded => new SolidColorBrush(Color.FromRgb(34, 139, 34)),
            DownloadStatus.Downloading => new SolidColorBrush(Color.FromRgb(30, 144, 255)),
            DownloadStatus.Failed => new SolidColorBrush(Color.FromRgb(220, 20, 60)),
            DownloadStatus.NoPdf => new SolidColorBrush(Color.FromRgb(169, 169, 169)),
            _ => new SolidColorBrush(Color.FromRgb(100, 100, 100))
        } : new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
