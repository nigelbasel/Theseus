using System.Diagnostics;
using Theseus.Core.Terminal;
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
    /// <param name="runningLaunchers">
    /// Where launchers are running from, so their kits are left alone. The running processes when
    /// nothing else is said.
    /// </param>
    public static string Prepare(
        string installedLauncherPath,
        string root,
        Func<IReadOnlyCollection<string>>? runningLaunchers = null)
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
                Discard(root, destination, (runningLaunchers ?? RunningLaunchers)());
                return copied;
            }

            Copy(source, destination);
            File.WriteAllText(Path.Combine(destination, CompletionMarker), string.Empty);

            TheseusLog.Information("Sessions will run the launcher copied to {Path}", copied);

            Discard(root, destination, (runningLaunchers ?? RunningLaunchers)());

            return copied;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Warning(exception,
                "The launcher could not be copied out of the installation, so sessions will hold it open");

            return installedLauncherPath;
        }
    }

    private static void Remove(string kit)
    {
        var aside = $"{kit}.{Guid.NewGuid():N}.discarding";

        try
        {
            Directory.Move(kit, aside);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("Left {Path} whole: something still holds a file in it", kit);

            return;
        }

        try
        {
            Directory.Delete(aside, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TheseusLog.Debug("Moved {Path} aside but could not delete it: {Reason}", kit, exception.Message);
        }
    }

    private static bool IsInside(string path, string directory)
    {
        var full = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar);

        return Path.GetFullPath(path)
            .StartsWith(full + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Where every running launcher is running from.</summary>
    private static IReadOnlyCollection<string> RunningLaunchers()
    {
        var paths = new List<string>();

        foreach (var process in Process.GetProcessesByName("Theseus.Launcher"))
        {
            using (process)
            {
                if (ProcessImagePath.For(process.Id) is { } path)
                {
                    paths.Add(path);
                }
            }
        }

        return paths;
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

    /// <summary>
    /// Clears away the kits of builds nothing is running any more.
    ///
    /// A kit in use must be left whole. Deleting one directly does not do that: a recursive delete
    /// removes every file it can and fails only on the ones Windows has locked, so a kit a session is
    /// running from is not skipped but gutted - and the session then dies the next time it needs an
    /// assembly it had not yet loaded. This is the same fault as an installer replacing files under a
    /// running session, which is what the kits exist to avoid, so it is worth two defences.
    ///
    /// The first is to skip any kit a launcher process is running from. The second is to rename a
    /// kit before deleting it: a directory holding an open file cannot be renamed, so anything still
    /// in use fails at the rename, with nothing deleted, whether or not its process could be found.
    /// </summary>
    private static void Discard(string root, string current, IReadOnlyCollection<string> inUse)
    {
        foreach (var stale in Directory.EnumerateDirectories(root)
                     .Where(directory => !string.Equals(directory, current, StringComparison.OrdinalIgnoreCase)))
        {
            if (inUse.Any(path => IsInside(path, stale)))
            {
                TheseusLog.Debug("Left {Path} alone: a session is running from it", stale);

                continue;
            }

            Remove(stale);
        }
    }
}
