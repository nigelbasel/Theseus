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
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ConsoleTitleReader : IConsoleTitleReader
{
    private const int MaximumTitleLength = 1024;

    private static readonly Lock Gate = new();

    /// <inheritdoc />
    public string? Read(int processId)
    {
        lock (Gate)
        {
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
            }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetConsoleTitleW", SetLastError = true)]
    private static extern int GetConsoleTitle(StringBuilder buffer, int size);
}
