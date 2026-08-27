using System.Runtime.InteropServices;
using System.Text;
using Theseus.Core.Diagnostics;

namespace Theseus.App.Services;

/// <summary>
/// Starts an idle session's turn by typing into its tab.
///
/// This is the one place Theseus synthesises input, and it exists because an idle session takes no
/// turn on its own: nothing fires the hook, so a request written for it would wait indefinitely.
///
/// What is typed is a marker, never the request. The hook supplies the request, so a keystroke that
/// somehow lands in the wrong window puts one meaningless word there rather than a paragraph of
/// instructions - a word that reads as "command not found" in a shell and as nothing much anywhere
/// else. Everything else is a gate: the session's own tab must be identified rather than merely its
/// window, and the terminal must still be in front at the instant of sending. Failing either, nothing
/// is sent and the board says so rather than assuming the session heard.
/// </summary>
public static class TabNudge
{
    private const string TerminalWindowClass = "CASCADIA_HOSTING_WINDOW_CLASS";
    private const int InputKeyboard = 1;
    private const uint KeyUp = 0x0002;
    private const uint Unicode = 0x0004;
    private const ushort Return = 0x0D;
    private const int AccessDenied = 5;

    /// <summary>Types the marker and Enter into one session's tab, if it can be confirmed.</summary>
    public static NudgeOutcome Nudge(int claudeProcessId, string? tabTitle, string marker)
    {
        if (TerminalFocus.FocusSession(claudeProcessId, tabTitle) is not FocusOutcome.TabSelected)
        {
            TheseusLog.Warning("Nothing was typed: the tab for process {Pid} could not be confirmed", claudeProcessId);

            return NudgeOutcome.TabNotConfirmed;
        }

        if (!TerminalIsInFront())
        {
            TheseusLog.Warning("Nothing was typed: the terminal was not in front when the moment came");

            return NudgeOutcome.FocusLost;
        }

        return Send(marker);
    }

    /// <summary>
    /// Whether the window about to receive the keystrokes is a terminal. Checked immediately before
    /// sending rather than once at the start: focus is the user's to move, and they may have moved it.
    /// </summary>
    private static bool TerminalIsInFront()
    {
        var foreground = GetForegroundWindow();

        if (foreground == IntPtr.Zero)
        {
            return false;
        }

        var name = new StringBuilder(64);

        return GetClassName(foreground, name, name.Capacity) > 0
               && string.Equals(name.ToString(), TerminalWindowClass, StringComparison.Ordinal);
    }

    private static NudgeOutcome Send(string marker)
    {
        var inputs = new List<Input>();

        foreach (var character in marker)
        {
            inputs.Add(Character(character, up: false));
            inputs.Add(Character(character, up: true));
        }

        inputs.Add(Key(Return, up: false));
        inputs.Add(Key(Return, up: true));

        var sent = SendInput((uint)inputs.Count, [.. inputs], Marshal.SizeOf<Input>());

        if (sent == inputs.Count)
        {
            return NudgeOutcome.Sent;
        }

        // Windows refuses input from a process to a window of higher integrity, and says so with
        // access denied. A terminal started as administrator is out of reach of a Theseus that was
        // not, and no amount of retrying changes that.
        if (Marshal.GetLastWin32Error() == AccessDenied)
        {
            TheseusLog.Warning("The terminal runs elevated and Theseus does not, so Windows refused the keystrokes");

            return NudgeOutcome.BlockedByElevation;
        }

        TheseusLog.Warning("Windows accepted {Sent} of {Total} keystrokes", sent, inputs.Count);

        return NudgeOutcome.Refused;
    }

    private static Input Character(char character, bool up) => new()
    {
        Type = InputKeyboard,
        Data = new InputData
        {
            Keyboard = new KeyboardInput
            {
                ScanCode = character,
                Flags = Unicode | (up ? KeyUp : 0),
            },
        },
    };

    private static Input Key(ushort virtualKey, bool up) => new()
    {
        Type = InputKeyboard,
        Data = new InputData
        {
            Keyboard = new KeyboardInput
            {
                VirtualKey = virtualKey,
                Flags = up ? KeyUp : 0,
            },
        },
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public int Type;
        public InputData Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputData
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetClassName(IntPtr window, StringBuilder name, int capacity);
}
