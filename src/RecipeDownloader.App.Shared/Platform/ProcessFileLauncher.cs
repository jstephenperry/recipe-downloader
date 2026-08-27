using System.Diagnostics;

namespace RecipeDownloader.App.Platform;

/// <summary>
/// Opens files and URLs through the operating system's shell. Works on all three desktop
/// platforms: <see cref="ProcessStartInfo.UseShellExecute"/> maps to ShellExecute on Windows,
/// <c>open</c> on macOS, and <c>xdg-open</c> on Linux.
/// </summary>
public class ProcessFileLauncher : IFileLauncher
{
    public void Open(string pathOrUrl)
    {
        if (string.IsNullOrWhiteSpace(pathOrUrl))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(pathOrUrl) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or FileNotFoundException)
        {
            // No handler is registered for this file type, or no desktop shell is present
            // (a headless Linux session, for instance). Failing to open a file should never
            // take the application down.
        }
    }
}
