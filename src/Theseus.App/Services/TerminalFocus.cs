using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using Theseus.Core.Diagnostics;
using Theseus.Core.Sessions;
using Theseus.Core.Terminal;

namespace Theseus.App.Services;

/// <summary>
/// Brings the terminal window hosting a session to the front.
///
/// The window is found by the tab it contains. Ownership is no help and neither is
/// <c>MainWindowHandle</c>: one <c>WindowsTerminal.exe</c> process hosts every window, so anything
/// that asks which window belongs to a process gets an arbitrary one. That is correct exactly while
/// there is one window, which is why it stood for so long, and wrong from the moment there are two.
/// </summary>
public static class TerminalFocus
{
    private const string TerminalWindowClass = "CASCADIA_HOSTING_WINDOW_CLASS";

    /// <summary>The class of the element that holds a session's text, and the only one that takes focus.</summary>
    private const string TerminalControlClass = "TermControl";

    private const int SwRestore = 9;

    /// <summary>
    /// Brings the terminal hosting the given Claude Code process to the front, and selects its tab
    /// when the tab can be identified.
    /// </summary>
    /// <param name="claudeProcessId">The session's process, where the walk to its window starts.</param>
    /// <param name="tabTitle">
    /// The tab's title, which is the only thing that identifies a tab: Windows Terminal exposes no
    /// per-tab identifier and reports the one terminal process for all of them. Sessions Theseus
    /// launched keep the title it gave them; for anything else this will not match, and the window
    /// is raised without changing the selected tab rather than selecting the wrong one.
    /// </param>
    /// <returns>What was achieved, so the caller can say so honestly.</returns>
    public static FocusOutcome FocusSession(int claudeProcessId, string? tabTitle = null)
    {
        var snapshot = ProcessSnapshot.Capture();
        var terminal = ProcessAncestry.TerminalOf(snapshot, claudeProcessId);
        var likeliest = terminal is { } processId ? FindWindow(processId) : null;

        // Search by what a window contains rather than by whose process it belongs to. One terminal
        // process hosts every window, so the process points at an arbitrary one - right while there
        // is only one window, and wrong the moment somebody opens a second. The window it points at
        // is tried first, so the ordinary case still costs one look.
        if (!string.IsNullOrWhiteSpace(tabTitle))
        {
            foreach (var candidate in WindowSearchOrder.PreferringFirst(likeliest, Terminals()))
            {
                if (SelectTab(candidate, tabTitle!) && Raise(candidate))
                {
                    return FocusOutcome.TabSelected;
                }
            }
        }

        // No tab of that name anywhere, or no name to look for. Raising the window the process
        // points at is the most that can honestly be done, and the caller is told it is only that.
        if (likeliest is { } window && Raise(window))
        {
            return FocusOutcome.WindowOnly;
        }

        return FocusOutcome.NotFound;
    }

    /// <summary>Brings a specific terminal process's window to the front.</summary>
    public static bool FocusTerminal(int terminalProcessId) =>
        FindWindow(terminalProcessId) is { } window && Raise(window);

    /// <summary>
    /// Selects the tab with the given title. Runs before the window is raised so the correct tab is
    /// already showing when it appears, rather than the window flicking from one tab to another.
    /// </summary>
    private static bool SelectTab(IntPtr window, string title)
    {
        try
        {
            var root = AutomationElement.FromHandle(window);
            if (root is null)
            {
                return false;
            }

            var tabs = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));

            foreach (AutomationElement tab in tabs)
            {
                if (!ConsoleTitle.Matches(title, tab.Current.Name))
                {
                    continue;
                }

                if (tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern)
                    && pattern is SelectionItemPattern selection)
                {
                    selection.Select();

                    return true;
                }
            }
        }
        catch (ElementNotAvailableException)
        {
            TheseusLog.Debug("The terminal window closed while its tabs were being read");
        }
        catch (InvalidOperationException exception)
        {
            TheseusLog.Warning(exception, "Could not select the tab titled {Title}", title);
        }
        catch (COMException exception)
        {
            TheseusLog.Warning(exception, "UI Automation refused while selecting the tab titled {Title}", title);
        }

        return false;
    }

    /// <summary>
    /// Puts keyboard focus on the terminal's text, which selecting a tab does not do.
    ///
    /// Selecting through UI Automation moves focus onto the tab in the strip: the right tab is
    /// showing, the window is in front, and anything typed goes to the tab header, where it is
    /// discarded. For a person reaching for the mouse this is invisible, because their next click
    /// lands in the text. For keystrokes synthesised straight afterwards it is the whole difference
    /// between a session that hears and one that does not - and it looks exactly like nothing having
    /// been sent at all.
    ///
    /// The buffer is a <c>Text</c> element whose class is <c>TermControl</c>, and it is found by that
    /// class and by asking whether it will take focus - not by its name, which is the profile's
    /// ("Default"), and not by its control type alone, which several unfocusable labels share.
    ///
    /// Both previous attempts guessed. The first searched for <c>Document</c>, which a terminal
    /// window does not expose at all. The second searched Text elements for one named after the tab,
    /// found none, and fell back to the first Text in the tree, which refused with "target element
    /// cannot receive focus". Asking the tree which elements are focusable answered it in a minute.
    /// </summary>
    /// <returns>Whether focus is now in a terminal buffer.</returns>
    public static bool FocusContent(nint window)
    {
        try
        {
            if (AutomationElement.FromHandle(window) is not { } root)
            {
                return false;
            }

            var candidates = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Text));

            foreach (AutomationElement candidate in candidates)
            {
                if (candidate.Current is { IsKeyboardFocusable: true, ClassName: TerminalControlClass })
                {
                    candidate.SetFocus();

                    return true;
                }
            }

            TheseusLog.Warning("No terminal buffer in window {Window} would take focus", window);

            return false;
        }
        catch (Exception exception)
            when (exception is ElementNotAvailableException or InvalidOperationException or COMException)
        {
            TheseusLog.Debug("Could not put focus on the terminal's text: {Reason}", exception.Message);

            return false;
        }
    }

    /// <summary>
    /// Whether keyboard focus is in a terminal buffer rather than on the tab strip. Selecting a tab
    /// leaves focus on the tab, where every keystroke is silently discarded, so this is the question
    /// worth asking before typing anything.
    /// </summary>
    public static bool FocusIsInTerminal()
    {
        try
        {
            return AutomationElement.FocusedElement?.Current.ClassName == TerminalControlClass;
        }
        catch (Exception exception)
            when (exception is ElementNotAvailableException or InvalidOperationException or COMException)
        {
            return false;
        }
    }

    /// <summary>
    /// What currently has keyboard focus, named for the log. Synthesised keystrokes go wherever this
    /// is, and when they vanish it is the only thing worth knowing.
    /// </summary>
    public static string FocusedElement()
    {
        try
        {
            if (AutomationElement.FocusedElement is not { } focused)
            {
                return "nothing";
            }

            return $"{focused.Current.ControlType.ProgrammaticName} \"{focused.Current.Name}\"";
        }
        catch (Exception exception)
            when (exception is ElementNotAvailableException or InvalidOperationException or COMException)
        {
            return $"unreadable ({exception.Message})";
        }
    }

    /// <summary>
    /// Every visible terminal window on the desktop, whichever process owns it. Ownership is not a
    /// useful filter here: they nearly all share one.
    /// </summary>
    private static IReadOnlyList<nint> Terminals()
    {
        var found = new List<nint>();

        EnumWindows((handle, _) =>
        {
            if (IsWindowVisible(handle) && ClassNameOf(handle) == TerminalWindowClass)
            {
                found.Add(handle);
            }

            return true;
        }, IntPtr.Zero);

        return found;
    }

    private static IntPtr? FindWindow(int processId)
    {
        IntPtr? found = null;

        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle))
            {
                return true;
            }

            GetWindowThreadProcessId(handle, out var owner);
            if (owner != processId || ClassNameOf(handle) != TerminalWindowClass)
            {
                return true;
            }

            found = handle;

            return false;
        }, IntPtr.Zero);

        return found;
    }

    private static bool Raise(IntPtr window)
    {
        if (IsIconic(window))
        {
            ShowWindow(window, SwRestore);
        }

        var foreground = GetForegroundWindow();
        var foregroundThread = GetWindowThreadProcessId(foreground, out _);
        var currentThread = GetCurrentThreadId();
        var attached = foregroundThread != currentThread
                       && AttachThreadInput(currentThread, foregroundThread, true);

        try
        {
            BringWindowToTop(window);

            return SetForegroundWindow(window);
        }
        finally
        {
            if (attached)
            {
                AttachThreadInput(currentThread, foregroundThread, false);
            }
        }
    }

    private static string ClassNameOf(IntPtr window)
    {
        var buffer = new StringBuilder(256);
        var length = GetClassName(window, buffer, buffer.Capacity);

        return length > 0 ? buffer.ToString(0, length) : string.Empty;
    }

    private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetClassNameW")]
    private static extern int GetClassName(IntPtr window, StringBuilder buffer, int capacity);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr window, int command);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(IntPtr window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint target, uint source, [MarshalAs(UnmanagedType.Bool)] bool attach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();
}
