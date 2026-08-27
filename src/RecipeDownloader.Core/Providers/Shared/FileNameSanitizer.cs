namespace RecipeDownloader.Core.Providers.Shared;

/// <summary>
/// Produces file names that are valid on every supported platform.
/// </summary>
/// <remarks>
/// <see cref="Path.GetInvalidFileNameChars"/> is platform-specific: on Unix it reports only
/// '/' and '\0', so a name sanitized on Linux or macOS can still contain characters (':', '?',
/// '*', '"', '&lt;', '&gt;', '|') that are illegal on Windows and on SMB/OneDrive/Dropbox shares.
/// This class uses a fixed superset so downloads written on one platform stay portable.
/// </remarks>
public static class FileNameSanitizer
{
    private const int MaxLength = 200;

    private static readonly char[] InvalidChars =
        ['/', '\\', ':', '*', '?', '"', '<', '>', '|', '\0'];

    /// <summary>
    /// Replaces characters that are invalid on any supported platform with '_' and
    /// trims the result to a length that is safe on all common file systems.
    /// </summary>
    public static string Sanitize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "recipe";

        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (char.IsControl(c) || Array.IndexOf(InvalidChars, c) >= 0)
                chars[i] = '_';
        }

        var sanitized = new string(chars).Trim();

        if (sanitized.Length > MaxLength)
            sanitized = sanitized[..MaxLength].Trim();

        // Windows rejects names that end in a dot or space, and reserves a handful of device names.
        sanitized = sanitized.TrimEnd('.', ' ');

        if (sanitized.Length == 0 || IsReservedDeviceName(sanitized))
            return "recipe";

        return sanitized;
    }

    private static bool IsReservedDeviceName(string name)
    {
        var stem = name;
        var dot = stem.IndexOf('.');
        if (dot >= 0)
            stem = stem[..dot];

        return stem.ToUpperInvariant() switch
        {
            "CON" or "PRN" or "AUX" or "NUL" => true,
            var s when s.Length == 4 && (s.StartsWith("COM") || s.StartsWith("LPT")) && char.IsDigit(s[3]) => true,
            _ => false
        };
    }
}
