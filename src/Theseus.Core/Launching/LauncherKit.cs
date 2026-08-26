using System.Diagnostics;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Launching;

/// <summary>
/// A copy of the launcher that sessions run instead of the installed one. A running tab holds its
/// launcher open, and an installer replacing a file that is held closes whatever holds it: the tab,
/// and the session in it, with nothing written down. Running from a copy leaves the installed files
/// unheld, so an upgrade costs the running sessions nothing and they pick up the new launcher when
/// they next start.
/// </summary>
public static class LauncherKit
{
    private const string CompletionMarker = ".complete";

    /// <summary>
    /// The launcher sessions should run, copying it out of the installation first if that has not
    /// already been done for this build. The installed launcher is returned unchanged if the copy
    /// cannot be made: holding a file open is a smaller problem than having no launcher at all.
    /// </summary>
    public static string Prepare(string installedLauncherPath, string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installedLauncherPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        if (!File.Exists(installedLauncherPath))
        {
            TheseusLog.Warning("There is no launcher at {Path} to copy", installedLauncherPath);
            return installedLauncherPath;
        }

        try
        {
            var source = Path.GetDirectoryName(installedLauncherPath)!;
            var destination = Path.Combine(root, Build(installedLauncherPath));
            var copied = Path.Combine(destination, Path.GetFileName(installedLauncherPath));

            if (File.Exists(Path.Combine(destination, CompletionMarker)) && File.Exists(copied))
            {
                Discard(root, destination);
                return copied;
            }

            Copy(source, destination);
            File.WriteAllText(Path.Combine(destination, CompletionMarker), string.Empty);

            TheseusLog.Information("Sessions will run the launcher copied to {Path}", copied);

            Discard(root, destination);

            return copied;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception,
                "The launcher could not be copied out of the installation, so sessions will hold it open");

            return installedLauncherPath;
        }
    }

    private static string Build(string launcherPath)
    {
        var version = FileVersionInfo.GetVersionInfo(launcherPath).FileVersion;

        return string.IsNullOrWhiteSpace(version)
            ? File.GetLastWriteTimeUtc(launcherPath).Ticks.ToString()
            : version;
    }

    private static void Copy(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Combine(destination, Path.GetRelativePath(source, file)), overwrite: true);
        }
    }

    private static void Discard(string root, string current)
    {
        foreach (var stale in Directory.EnumerateDirectories(root)
                     .Where(directory => !string.Equals(directory, current, StringComparison.OrdinalIgnoreCase)))
        {
            try
            {
                Directory.Delete(stale, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                TheseusLog.Debug("Left {Path} alone: a session is still running it", stale);
            }
        }
    }
}
