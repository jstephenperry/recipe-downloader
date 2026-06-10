using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using RecipeDownloader.ViewModels;

namespace RecipeDownloader.WinUI.Converters;

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        var invert = parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase);
        var boolValue = value is true or int and not 0;
        if (invert) boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        var invert = parameter is string s && s.Equals("invert", StringComparison.OrdinalIgnoreCase);
        var visible = value is not null;
        if (invert) visible = !visible;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}

/// <summary>Visible when the bound string equals the converter parameter. Used for view switching.</summary>
public class StringEqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        return string.Equals(value as string, parameter as string, StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}

public class DownloadStatusToTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
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

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}

public class DownloadStatusToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, string language)
    {
        return value is DownloadStatus status ? status switch
        {
            DownloadStatus.Downloaded => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 34, 139, 34)),
            DownloadStatus.Downloading => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 144, 255)),
            DownloadStatus.Failed => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 220, 20, 60)),
            DownloadStatus.NoPdf => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 169, 169, 169)),
            _ => new SolidColorBrush(Windows.UI.Color.FromArgb(255, 100, 100, 100))
        } : new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, string language)
        => throw new NotSupportedException();
}
