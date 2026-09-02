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
    private const ushort Escape = 0x1B;
    private const int AccessDenied = 5;

    private static readonly TimeSpan SettlingTime = TimeSpan.FromMilliseconds(200);

    /// <summary>Types the marker and Enter into one session's tab, if it can be confirmed.</summary>
    /// <param name="interruptFirst">
    /// Whether to cut the current turn short before typing, the way a person would. A working
    /// session is not going to read a prompt until it stops working, and an interrupted turn does
    /// not end in the way that fires the stop hook - so the two go together or neither is any use.
    /// </param>
    public static NudgeOutcome Nudge(int claudeProcessId, string? tabTitle, string marker,
        bool interruptFirst = false)
    {
        if (TerminalFocus.FocusSession(claudeProcessId, tabTitle) is not FocusOutcome.TabSelected)
        {
            TheseusLog.Warning("Nothing was typed: the tab for process {Pid} could not be confirmed", claudeProcessId);

            return NudgeOutcome.TabNotConfirmed;
        }

        // Let the window settle after being raised before typing into it. Raising is asynchronous:
        // the call returns before the window has taken keyboard focus, and keystrokes sent into that
        // gap go nowhere.
        //
        // This was being paid for by accident. The line below that reads the focused element costs a
        // UI Automation round trip, and that delay - not the code that was supposed to be doing the
        // work - is the likeliest reason the first successful nudge succeeded. A diagnostic that
        // load-bears is a diagnostic somebody deletes.
        Thread.Sleep(SettlingTime);

        var foreground = GetForegroundWindow();

        if (!TerminalIsInFront())
        {
            TheseusLog.Warning("Nothing was typed: the terminal was not in front when the moment came");

            return NudgeOutcome.FocusLost;
        }

        // Selecting a tab leaves focus on the tab strip, where every keystroke is silently
        // discarded, so focus is moved into the buffer here rather than hoped for. It happens after
        // the settle, not before: the newly selected tab's control does not exist until the terminal
        // has drawn it, and there is nothing to focus until it does.
        TerminalFocus.FocusContent(foreground);

        // Where the keystrokes are about to go. Windows reporting that it accepted them says only
        // that they were injected, never that anything read them, and the difference is invisible
        // afterwards: a session that was never typed into looks the same as one that was typed into
        // somewhere nobody was listening.
        TheseusLog.Information("Typing into whatever has focus, which is {Focused}", TerminalFocus.FocusedElement());

        if (!TerminalFocus.FocusIsInTerminal())
        {
            TheseusLog.Warning("Nothing was typed: focus is not in a terminal buffer, so it would be discarded");

            return NudgeOutcome.FocusLost;
        }

        if (interruptFirst && !Interrupt())
        {
            return NudgeOutcome.Refused;
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

    /// <summary>
    /// Presses Escape, which is what stops a session mid-turn, and waits for it to take effect.
    ///
    /// The pause is not decoration. The marker typed straight afterwards has to arrive at a prompt
    /// that is accepting one, and a session still unwinding its turn will swallow it or, worse,
    /// take it as an answer to something. A fifth of a second is what a person's hand takes anyway.
    /// </summary>
    private static bool Interrupt()
    {
        var inputs = new List<Input> { Key(Escape, up: false), Key(Escape, up: true) };
        var sent = SendInput((uint)inputs.Count, [.. inputs], Marshal.SizeOf<Input>());

        if (sent != inputs.Count)
        {
            TheseusLog.Warning("The turn could not be interrupted: Windows reported error {Error}",
                Marshal.GetLastWin32Error());

            return false;
        }

        Thread.Sleep(SettlingTime);

        return true;
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

        var error = Marshal.GetLastWin32Error();

        // Windows refuses input from a process to a window of higher integrity, and says so with
        // access denied. A terminal started as administrator is out of reach of a Theseus that was
        // not, and no amount of retrying changes that.
        if (error == AccessDenied)
        {
            TheseusLog.Warning("The terminal runs elevated and Theseus does not, so Windows refused the keystrokes");

            return NudgeOutcome.BlockedByElevation;
        }

        // The number matters. Every refusal reads the same from the outside - nothing is typed and
        // the session sits there - and only the code tells a locked-out desktop from a structure
        // Windows would not accept, which is what this turned out to be the first time.
        TheseusLog.Warning("Windows accepted {Sent} of {Total} keystrokes and reported error {Error}",
            sent, inputs.Count, error);

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
    internal struct Input
    {
        public int Type;
        public InputData Data;
    }

    /// <summary>
    /// The union inside INPUT, sized to the largest member Windows knows about rather than to the
    /// only one declared here.
    ///
    /// Declaring just the keyboard member makes it 24 bytes, and INPUT 32 rather than the 40 an
    /// x64 Windows expects. SendInput checks the size it is handed against its own and does nothing
    /// at all when they disagree - no exception, no access denied, just zero events accepted. It
    /// looked exactly like a permissions problem and was not one: nothing was ever typed into any
    /// session, on any machine, by any build.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    internal struct InputData
    {
        [FieldOffset(0)]
        public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct KeyboardInput
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
