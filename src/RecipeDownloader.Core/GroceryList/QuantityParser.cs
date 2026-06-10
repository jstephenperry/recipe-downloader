using System.Globalization;
using System.Text.RegularExpressions;

namespace RecipeDownloader.Core.GroceryList;

/// <summary>
/// Parses quantity strings (including Unicode fractions) into decimals and formats results.
/// </summary>
public static partial class QuantityParser
{
    private static readonly Dictionary<char, double> UnicodeFractions = new()
    {
        ['½'] = 0.5, ['⅓'] = 1.0 / 3, ['⅔'] = 2.0 / 3,
        ['¼'] = 0.25, ['¾'] = 0.75,
        ['⅕'] = 0.2, ['⅖'] = 0.4, ['⅗'] = 0.6, ['⅘'] = 0.8,
        ['⅙'] = 1.0 / 6, ['⅚'] = 5.0 / 6,
        ['⅛'] = 0.125, ['⅜'] = 0.375, ['⅝'] = 0.625, ['⅞'] = 0.875
    };

    // Unit conversion factors to a common base
    private static readonly Dictionary<string, (string BaseUnit, double Factor)> UnitConversions = new(StringComparer.OrdinalIgnoreCase)
    {
        // Weight: base = oz
        ["oz"] = ("oz", 1.0),
        ["ounce"] = ("oz", 1.0),
        ["lb"] = ("oz", 16.0),
        ["lbs"] = ("oz", 16.0),
        ["pound"] = ("oz", 16.0),

        // Volume small: base = tsp
        ["tsp"] = ("tsp", 1.0),
        ["teaspoon"] = ("tsp", 1.0),
        ["tbsp"] = ("tsp", 3.0),
        ["tablespoon"] = ("tsp", 3.0),

        // Volume large: base = cup
        ["cup"] = ("cup", 1.0),
        ["cups"] = ("cup", 1.0),
    };

    public static double? Parse(string? quantity)
    {
        if (string.IsNullOrWhiteSpace(quantity))
            return null;

        var text = quantity.Trim();
        double result = 0;

        // Handle mixed numbers like "1 ½" or "2½"
        var match = MixedNumberRegex().Match(text);
        if (match.Success)
        {
            result = double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var fracChar = match.Groups[2].Value[0];
            if (UnicodeFractions.TryGetValue(fracChar, out var fracVal))
                result += fracVal;
            return result;
        }

        // Single unicode fraction
        if (text.Length == 1 && UnicodeFractions.TryGetValue(text[0], out var singleFrac))
            return singleFrac;

        // Slash fraction like "1/2" or "3/4"
        var slashMatch = SlashFractionRegex().Match(text);
        if (slashMatch.Success)
        {
            var whole = slashMatch.Groups[1].Success ? double.Parse(slashMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
            var num = double.Parse(slashMatch.Groups[2].Value, CultureInfo.InvariantCulture);
            var den = double.Parse(slashMatch.Groups[3].Value, CultureInfo.InvariantCulture);
            return den != 0 ? whole + num / den : null;
        }

        // Plain number
        if (double.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var plainNum))
            return plainNum;

        return null;
    }

    public static string Format(double value)
    {
        if (value <= 0) return "0";

        var whole = (int)value;
        var frac = value - whole;

        // Try to express as a clean fraction
        var fracStr = frac switch
        {
            < 0.05 => null,
            < 0.2 => "1/8",
            < 0.29 => "1/4",
            < 0.375 => "1/3",
            < 0.458 => "1/2",  // adjusted threshold
            < 0.583 => "1/2",
            < 0.708 => "2/3",
            < 0.792 => "3/4",
            < 0.917 => "7/8",
            _ => null // rounds up to next whole
        };

        if (frac >= 0.917)
            whole++;

        if (whole > 0 && fracStr is not null)
            return $"{whole} {fracStr}";
        if (whole > 0)
            return whole.ToString();
        if (fracStr is not null)
            return fracStr;
        return Math.Round(value, 2).ToString();
    }

    public static (double? CombinedQuantity, string? Unit) CombineQuantities(
        string? qty1, string? unit1,
        string? qty2, string? unit2)
    {
        var val1 = Parse(qty1);
        var val2 = Parse(qty2);

        if (val1 is null && val2 is null)
            return (null, unit1 ?? unit2);
        if (val1 is null)
            return (val2, unit2);
        if (val2 is null)
            return (val1, unit1);

        var u1 = NormalizeUnit(unit1);
        var u2 = NormalizeUnit(unit2);

        // Same unit or one is null — just add
        if (u1 == u2 || u1 is null || u2 is null)
            return (val1.Value + val2.Value, unit1 ?? unit2);

        // Try converting to common base
        if (UnitConversions.TryGetValue(u1, out var conv1) &&
            UnitConversions.TryGetValue(u2, out var conv2) &&
            conv1.BaseUnit == conv2.BaseUnit)
        {
            var totalInBase = val1.Value * conv1.Factor + val2.Value * conv2.Factor;
            // Express in the larger unit if possible
            var (bestUnit, bestFactor) = conv1.Factor >= conv2.Factor ? (unit1, conv1.Factor) : (unit2, conv2.Factor);
            return (totalInBase / bestFactor, bestUnit);
        }

        // Incompatible units — return the first quantity and note both
        return (val1, unit1);
    }

    private static string? NormalizeUnit(string? unit)
    {
        if (string.IsNullOrWhiteSpace(unit))
            return null;
        var u = unit.Trim().ToLowerInvariant().TrimEnd('.');
        // Strip trailing 's' for plurals
        if (u.EndsWith('s') && u is not "lbs")
            u = u[..^1];
        return u;
    }

    [GeneratedRegex(@"^(\d+)\s*([½⅓⅔¼¾⅕⅖⅗⅘⅙⅚⅛⅜⅝⅞])$")]
    private static partial Regex MixedNumberRegex();

    [GeneratedRegex(@"^(?:(\d+)\s+)?(\d+)/(\d+)$")]
    private static partial Regex SlashFractionRegex();
}
