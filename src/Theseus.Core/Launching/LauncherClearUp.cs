using System.Diagnostics;
using System.Runtime.Versioning;
using Theseus.Core.Diagnostics;
using Theseus.Core.Sessions;
using Theseus.Core.Terminal;

namespace Theseus.Core.Launching;

/// <summary>
/// Ends, at start-up, the launchers <see cref="StrayLaunchers"/> finds: each one alone, never the process tree and never the
/// terminal that hosts it, and each one logged.
/// </summary>
[SupportedOSPlatform("windows")]
public static class LauncherClearUp
{
    private const string LauncherName = "Theseus.Launcher";

    /// <summary>Ends the launchers under <paramref name="launcherRoot"/> that have outlived their session; how many.</summary>
    /// <param name="launcherRoot">The folder Theseus copies its launchers into.</param>
    /// <param name="currentLauncher">The launcher this release's sessions run.</param>
    /// <param name="now">The time now.</param>
    public static int Run(string launcherRoot, string currentLauncher, DateTimeOffset now)
    {
        var parents = ProcessSnapshot.Capture().Select(entry => entry.ParentProcessId).ToHashSet();
        var running = new List<LauncherProcess>();
        var processes = Process.GetProcessesByName(LauncherName);

        try
        {
            foreach (var process in processes)
            {
                if (Describe(process, parents) is { } launcher)
                {
                    running.Add(launcher);
                }
            }

            var cleared = 0;

            foreach (var stray in StrayLaunchers.Among(running, launcherRoot, Path.GetDirectoryName(currentLauncher) ?? launcherRoot, now))
            {
                if (End(processes.First(process => process.Id == stray.ProcessId), stray))
                {
                    cleared++;
                }
            }

            return cleared;
        }
        finally
        {
            foreach (var process in processes)
            {
                process.Dispose();
            }
        }
    }

    private static LauncherProcess? Describe(Process process, HashSet<int> parents)
    {
        try
        {
            return ProcessImagePath.For(process.Id) is { } path
                ? new LauncherProcess(process.Id, path, new DateTimeOffset(process.StartTime.ToUniversalTime(), TimeSpan.Zero),
                    parents.Contains(process.Id))
                : null;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            TheseusLog.Debug("Could not read launcher {Pid}: {Reason}", process.Id, exception.Message);

            return null;
        }
    }

    private static bool End(Process process, LauncherProcess stray)
    {
        try
        {
            process.Kill(entireProcessTree: false);
            TheseusLog.Information("Cleared up launcher {Pid} from {Path}, started {Started:yyyy-MM-dd HH:mm:ss}: its session had ended",
                stray.ProcessId, stray.ImagePath, stray.StartedUtc.ToLocalTime());

            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            TheseusLog.Warning("Could not clear up launcher {Pid} from {Path}: {Reason}", stray.ProcessId, stray.ImagePath, exception.Message);

            return false;
        }
    }
}
