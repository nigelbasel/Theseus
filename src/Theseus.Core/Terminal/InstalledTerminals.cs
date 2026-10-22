using System.Runtime.Versioning;
using Microsoft.Win32;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Terminal;

/// <summary>
/// The Windows Terminal packages installed for this user, whether or not any is running.
///
/// This exists for the first launch after a restart, when nothing is running to follow. The
/// <c>wt.exe</c> alias is the obvious fallback and the wrong one: both packages claim it, so on a
/// machine with the preview installed it opens the preview - which is how tabs kept appearing in a
/// terminal nobody had asked for.
///
/// The package folder cannot be listed directly, being under Program Files\WindowsApps, but each
/// registration records its own root under the user's own hive, which needs no elevation.
/// </summary>
public static class InstalledTerminals
{
    private const string Packages =
        @"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";

    private const string PackagePrefix = "Microsoft.WindowsTerminal";

    /// <summary>Every installed terminal executable, stable before preview.</summary>
    public static IReadOnlyList<string> Paths()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        try
        {
            using var packages = Registry.CurrentUser.OpenSubKey(Packages);

            if (packages is null)
            {
                return [];
            }

            var executables = new List<string>();

            foreach (var name in packages.GetSubKeyNames())
            {
                if (name.StartsWith(PackagePrefix, StringComparison.OrdinalIgnoreCase)
                    && Executable(packages, name) is { } executable)
                {
                    executables.Add(executable);
                }
            }

            return
            [
                .. executables
                    .OrderBy(TerminalSelector.IsPreview)
                    .ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase)
            ];
        }
        catch (Exception exception) when (exception is System.Security.SecurityException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("The installed terminals could not be listed: {Reason}", exception.Message);

            return [];
        }
    }

    [SupportedOSPlatform("windows")]
    private static string? Executable(RegistryKey packages, string name)
    {
        using var package = packages.OpenSubKey(name);

        if (package?.GetValue("PackageRootFolder") is not string root)
        {
            return null;
        }

        var executable = Path.Combine(root, "WindowsTerminal.exe");

        return File.Exists(executable) ? executable : null;
    }
}
