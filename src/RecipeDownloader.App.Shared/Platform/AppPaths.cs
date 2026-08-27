namespace RecipeDownloader.App.Platform;

/// <summary>
/// Resolves the application's storage locations on whichever platform it is running.
/// </summary>
/// <remarks>
/// The .NET special folders already map to each platform's convention:
/// <c>LocalApplicationData</c> is <c>%LocalAppData%</c> on Windows,
/// <c>~/Library/Application Support</c> on macOS, and <c>$XDG_DATA_HOME</c> (usually
/// <c>~/.local/share</c>) on Linux.
/// </remarks>
public static class AppPaths
{
    private const string AppFolderName = "RecipeDownloader";

    /// <summary>
    /// Where settings, cached catalogs, and the pantry are stored.
    /// </summary>
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName);

    public static string SettingsPath { get; } = Path.Combine(DataDirectory, "settings.json");

    /// <summary>
    /// The output folder proposed the first time the application runs. On Windows and macOS
    /// this lands under the user's Documents folder; on Linux, where .NET reports the home
    /// directory for <c>MyDocuments</c>, it lands directly in <c>~/Recipes</c>.
    /// </summary>
    public static string DefaultOutputDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Recipes");
}
