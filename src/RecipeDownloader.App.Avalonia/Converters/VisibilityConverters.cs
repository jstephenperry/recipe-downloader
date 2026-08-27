using System.Globalization;
using Avalonia.Data.Converters;

namespace RecipeDownloader.App.Avalonia.Converters;

/// <summary>
/// Maps a value to <c>IsVisible</c>. Avalonia has no Visibility enum — controls expose a
/// boolean <c>IsVisible</c> — so these converters return bool rather than a visibility value.
/// </summary>
public class TruthyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var truthy = value switch
        {
            null => false,
            bool b => b,
            int i => i != 0,
            string s => !string.IsNullOrWhiteSpace(s),
            _ => true
        };

        if (parameter is string p && p.Equals("invert", StringComparison.OrdinalIgnoreCase))
            truthy = !truthy;

        return truthy;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// True when the bound value is not null. Used to swap between a placeholder and real content.
/// </summary>
public class NotNullConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var notNull = value is not null;

        if (parameter is string p && p.Equals("invert", StringComparison.OrdinalIgnoreCase))
            notNull = !notNull;

        return notNull;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// True when the bound value equals the converter parameter. This replaces the WPF
/// <c>DataTrigger</c> machinery the original views used to switch between screens —
/// Avalonia styles use selectors and have no direct DataTrigger equivalent.
/// </summary>
public class StringEqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.Equals(value as string, parameter as string, StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
