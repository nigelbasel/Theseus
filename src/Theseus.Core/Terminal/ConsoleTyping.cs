using System.Runtime.Versioning;
using System.Runtime.InteropServices;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Terminal;

/// <summary>
/// Types a line into one session's console by writing to its input buffer, the way a keyboard would. Nothing is raised
/// and focus never moves, so it is the route for anything Theseus does on its own; and it cannot land in another window,
/// because it writes to that console and no other.
/// <para>
/// A process belongs to one console at a time, so this joins the session's console, writes, and leaves it, under a lock.
/// A caller that had a console of its own loses it: Theseus has none. While joined, a Ctrl+C or Ctrl+Break pressed in
/// that tab reaches Theseus too, and would end it, so a handler that ignores them is installed for the moment.
/// </para>
/// </summary>
[SupportedOSPlatform("windows")]
public static class ConsoleTyping
{
    private const ushort KeyEvent = 1;
    private const uint GenericReadWrite = 0xC0000000;
    private const uint ShareReadWrite = 3;
    private const uint OpenExisting = 3;
    private static readonly IntPtr InvalidHandle = new(-1);
    private const uint LeftCtrlPressed = 0x0008;
    private static readonly object Gate = new();
    private static readonly ConsoleCtrlHandler Ignore = _ => true;

    /// <summary>Types <paramref name="line"/> and Enter into the console of process <paramref name="processId"/>.</summary>
    public static ConsoleTypingOutcome TypeLine(int processId, string line)
    {
        var keys = ConsoleKeystrokes.ForLine(line);
        var records = keys.Select(Record).ToArray();

        lock (Gate)
        {
            FreeConsole();
            SetConsoleCtrlHandler(Ignore, true);

            if (!AttachConsole((uint)processId))
            {
                SetConsoleCtrlHandler(Ignore, false);
                TheseusLog.Debug("Could not join the console of process {Pid}: error {Error}", processId, Marshal.GetLastWin32Error());

                return ConsoleTypingOutcome.NotAttached;
            }

            try
            {
                var input = CreateFileW("CONIN$", GenericReadWrite, ShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

                if (input == InvalidHandle)
                {
                    TheseusLog.Debug("Could not open the input of process {Pid}'s console: error {Error}", processId, Marshal.GetLastWin32Error());

                    return ConsoleTypingOutcome.NoInput;
                }

                try
                {
                    var written = WriteConsoleInputW(input, records, (uint)records.Length, out var count) ? count : 0;

                    return written == records.Length ? ConsoleTypingOutcome.Sent : ConsoleTypingOutcome.PartlyWritten;
                }
                finally
                {
                    CloseHandle(input);
                }
            }
            finally
            {
                FreeConsole();
                SetConsoleCtrlHandler(Ignore, false);
            }
        }
    }

    private static InputRecord Record(ConsoleKeystroke key) => new()
    {
        EventType = KeyEvent,
        Key = new KeyEventRecord
        {
            KeyDown = key.IsDown ? 1 : 0,
            RepeatCount = 1,
            VirtualKeyCode = key.VirtualKey,
            UnicodeChar = key.Character,
            ControlKeyState = key.WithControl ? LeftCtrlPressed : 0,
        },
    };

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeConsole();

    private delegate bool ConsoleCtrlHandler(uint controlType);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlHandler handler, [MarshalAs(UnmanagedType.Bool)] bool add);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags,
        IntPtr template);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool WriteConsoleInputW(IntPtr input, InputRecord[] records, uint length, out uint written);

    [StructLayout(LayoutKind.Explicit, CharSet = CharSet.Unicode)]
    private struct KeyEventRecord
    {
        [FieldOffset(0)] public int KeyDown;
        [FieldOffset(4)] public ushort RepeatCount;
        [FieldOffset(6)] public ushort VirtualKeyCode;
        [FieldOffset(8)] public ushort VirtualScanCode;
        [FieldOffset(10)] public char UnicodeChar;
        [FieldOffset(12)] public uint ControlKeyState;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputRecord
    {
        [FieldOffset(0)] public ushort EventType;
        [FieldOffset(4)] public KeyEventRecord Key;
    }
}
