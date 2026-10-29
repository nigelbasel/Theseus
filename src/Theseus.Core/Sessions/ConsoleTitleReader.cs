using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Sessions;

/// <summary>
/// Reads the console title of another process by attaching to its console.
///
/// This is the only thing that ties a session to the terminal tab showing it. Windows Terminal
/// exposes no per-tab identifier and reports one process for every tab, so the title the session
/// writes to its own console is the sole shared value between what Theseus knows and what the
/// operator sees on the tab.
///
/// A process may hold only one console at a time, so reads are serialised and the console is always
/// released again.
///
/// Attaching also points this process's standard handles at the borrowed console, and releasing it
/// leaves them pointing at handles that are now closed. Every later Process.Start without redirection
/// passes them on, which .NET 11 rejects as invalid, so they are put back as they were.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ConsoleTitleReader : IConsoleTitleReader
{
    private const int MaximumTitleLength = 1024;

    private static readonly int[] StandardHandles = [-10, -11, -12];

    private static readonly Lock Gate = new();

    /// <inheritdoc />
    public string? Read(int processId)
    {
        lock (Gate)
        {
            var saved = StandardHandles.Select(GetStdHandle).ToArray();

            if (!AttachConsole(processId))
            {
                TheseusLog.Debug("Could not attach to the console of process {Pid}: win32 {Error}",
                    processId, Marshal.GetLastWin32Error());

                return null;
            }

            try
            {
                var buffer = new StringBuilder(MaximumTitleLength);
                var length = GetConsoleTitle(buffer, MaximumTitleLength);

                return length > 0 ? ConsoleTitle.Clean(buffer.ToString()) : null;
            }
            finally
            {
                FreeConsole();

                for (var index = 0; index < StandardHandles.Length; index++)
                {
                    SetStdHandle(StandardHandles[index], saved[index]);
                }
            }
        }
    }

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetStdHandle(int which);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetStdHandle(int which, IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetConsoleTitleW", SetLastError = true)]
    private static extern int GetConsoleTitle(StringBuilder buffer, int size);
}
