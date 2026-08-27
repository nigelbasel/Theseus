using System.Runtime.InteropServices;
using System.Diagnostics;
using System.IO;
using Theseus.Core.Diagnostics;

namespace Theseus.App.Services;

/// <summary>
/// Keeps one Theseus to a desktop.
///
/// Two are worse than useless. Each serves the same named pipe and keeps its launch tickets in its
/// own memory, so a tab opened by one is answered by whichever the shim happens to reach - and that
/// one has never heard of the ticket, refuses it, and the tab dies holding an error. They also both
/// watch the registry and both write the board, so the last to save wins.
///
/// A second copy therefore hands its turn to the first: it brings that window forward, which is what
/// someone starting the application again is asking for anyway, and leaves.
/// </summary>
public static class SingleInstance
{
    private const string Name = @"Local\Theseus.Board";

    private static Mutex? held;

    /// <summary>Whether this process is the one that should run.</summary>
    public static bool Claim()
    {
        try
        {
            held = new Mutex(initiallyOwned: true, Name, out var first);

            if (first)
            {
                return true;
            }

            held.Dispose();
            held = null;
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            TheseusLog.Warning(exception, "Could not check for another Theseus, so this one will run");

            return true;
        }

        TheseusLog.Information("Another Theseus is already running, so this one is bringing it forward");
        Raise();

        return false;
    }

    /// <summary>Brings the window of the copy that is already running to the front.</summary>
    private static void Raise()
    {
        var self = Environment.ProcessId;

        foreach (var process in Process.GetProcessesByName("Theseus"))
        {
            using (process)
            {
                if (process.Id == self || process.MainWindowHandle == IntPtr.Zero)
                {
                    continue;
                }

                ShowWindow(process.MainWindowHandle, RestoreIfMinimised);
                SetForegroundWindow(process.MainWindowHandle);

                return;
            }
        }
    }

    private const int RestoreIfMinimised = 9;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);
}
