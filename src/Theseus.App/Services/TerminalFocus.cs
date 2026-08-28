using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using Theseus.Core.Diagnostics;
using Theseus.Core.Sessions;

namespace Theseus.App.Services;

/// <summary>
/// Brings the terminal window hosting a session to the front. The window is found by class name and
/// owning process, never through <c>MainWindowHandle</c>: one <c>WindowsTerminal.exe</c> process
/// hosts every window, so that property names an arbitrary one of them.
/// </summary>
public static class TerminalFocus
{
    private const string TerminalWindowClass = "CASCADIA_HOSTING_WINDOW_CLASS";
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

        if (ProcessAncestry.TerminalOf(snapshot, claudeProcessId) is not { } terminalProcessId)
        {
            return FocusOutcome.NotFound;
        }

        if (FindWindow(terminalProcessId) is not { } window)
        {
            return FocusOutcome.NotFound;
        }

        var selected = !string.IsNullOrWhiteSpace(tabTitle) && SelectTab(window, tabTitle!);

        if (!Raise(window))
        {
            return FocusOutcome.NotFound;
        }

        return selected ? FocusOutcome.TabSelected : FocusOutcome.WindowOnly;
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
                    FocusContent(window, title);

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
    /// </summary>
    private static void FocusContent(IntPtr window, string title)
    {
        try
        {
            if (AutomationElement.FromHandle(window) is not { } root)
            {
                return;
            }

            var panes = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));

            AutomationElement? fallback = null;

            foreach (AutomationElement pane in panes)
            {
                fallback ??= pane;

                if (ConsoleTitle.Matches(title, pane.Current.Name))
                {
                    pane.SetFocus();

                    return;
                }
            }

            fallback?.SetFocus();
        }
        catch (Exception exception)
            when (exception is ElementNotAvailableException or InvalidOperationException or COMException)
        {
            TheseusLog.Debug("Could not put focus on the terminal's text: {Reason}", exception.Message);
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
