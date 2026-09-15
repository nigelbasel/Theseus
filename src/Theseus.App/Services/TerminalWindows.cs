using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using Theseus.Core.Diagnostics;
using Theseus.Core.Layout;

namespace Theseus.App.Services;

/// <summary>
/// Every terminal window on the desktop, with where it is and what it holds.
///
/// The work area is read through <c>GetMonitorInfo</c> rather than through the framework's screen
/// list, and that is deliberate. Window rectangles, monitor rectangles and <c>SetWindowPos</c> all
/// speak whichever coordinate space this process's DPI awareness puts them in, and they agree with
/// each other whatever that space turns out to be. Mixing in a source that does its own scaling is
/// how a layout ends up half a screen out - which a probe demonstrated, reporting the same desktop
/// as 2304 wide and 3456 wide depending on nothing but which framework was linked in.
/// </summary>
public static class TerminalWindows
{
    private const string TerminalWindowClass = "CASCADIA_HOSTING_WINDOW_CLASS";
    private const int MonitorDefaultToNearest = 2;

    /// <summary>Every visible terminal window, in the desktop's own z-order.</summary>
    /// <param name="withTabs">
    /// Whether to read each window's tabs. That is a UI Automation walk per window and is the
    /// expensive part, so it is asked for rather than assumed.
    /// </param>
    public static IReadOnlyList<TerminalWindow> All(bool withTabs = true)
    {
        var found = new List<TerminalWindow>();

        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || ClassNameOf(handle) != TerminalWindowClass)
            {
                return true;
            }

            GetWindowRect(handle, out var rect);

            found.Add(new TerminalWindow(
                handle,
                new LayoutRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top),
                IsIconic(handle),
                withTabs ? TabsOf(handle) : []));

            return true;
        }, nint.Zero);

        return found;
    }

    /// <summary>
    /// The usable area of the monitor a window is on — the screen less the taskbar. Taken from the
    /// window rather than from the primary monitor so that arranging happens where the work is.
    /// </summary>
    public static LayoutRect WorkAreaFor(nint window)
    {
        var monitor = MonitorFromWindow(window, MonitorDefaultToNearest);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

        if (monitor == nint.Zero || !GetMonitorInfo(monitor, ref info))
        {
            TheseusLog.Warning("The monitor holding window {Window} could not be read", window);

            return new LayoutRect(0, 0, 0, 0);
        }

        return new LayoutRect(
            info.WorkArea.Left,
            info.WorkArea.Top,
            info.WorkArea.Right - info.WorkArea.Left,
            info.WorkArea.Bottom - info.WorkArea.Top);
    }

    /// <summary>
    /// The titles of one window's tabs. Reads names only, where <see cref="TerminalFocus"/> needs the
    /// elements themselves to select one; the walk is the same and the purposes are not.
    /// </summary>
    private static IReadOnlyList<string> TabsOf(nint window)
    {
        try
        {
            if (AutomationElement.FromHandle(window) is not { } root)
            {
                return [];
            }

            var tabs = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));

            return [.. tabs.Cast<AutomationElement>().Select(tab => tab.Current.Name)];
        }
        catch (Exception exception)
            when (exception is ElementNotAvailableException or InvalidOperationException or COMException)
        {
            // A window running elevated cannot be read from here, and one that closed mid-walk is
            // simply gone. Neither is worth a warning: the caller sees a window holding nothing.
            TheseusLog.Debug("The tabs of window {Window} could not be read: {Reason}", window, exception.Message);

            return [];
        }
    }

    /// <summary>
    /// The title of the tab selected in one terminal window, or null when it cannot be read - an
    /// elevated window, one that closed mid-walk, or a tab strip with nothing selected.
    /// </summary>
    public static string? SelectedTab(nint window)
    {
        try
        {
            if (AutomationElement.FromHandle(window) is not { } root)
            {
                return null;
            }

            var tabs = root.FindAll(
                TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem));

            foreach (AutomationElement tab in tabs)
            {
                if (tab.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var pattern)
                    && pattern is SelectionItemPattern { Current.IsSelected: true })
                {
                    return tab.Current.Name;
                }
            }

            return null;
        }
        catch (Exception exception)
            when (exception is ElementNotAvailableException or InvalidOperationException or COMException)
        {
            TheseusLog.Debug("The selected tab of window {Window} could not be read: {Reason}", window, exception.Message);

            return null;
        }
    }

    /// <summary>Whether a window is a terminal window, by its class.</summary>
    public static bool IsTerminal(nint window) =>
        window != nint.Zero && ClassNameOf(window) == TerminalWindowClass;

    private static string ClassNameOf(nint window)
    {
        var name = new StringBuilder(64);

        return GetClassName(window, name, name.Capacity) > 0 ? name.ToString() : string.Empty;
    }

    private delegate bool EnumWindowsProc(nint window, nint parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint window, StringBuilder name, int capacity);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint window);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern nint MonitorFromWindow(nint window, int flags);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int Size;
        public Rect Monitor;
        public Rect WorkArea;
        public uint Flags;
    }
}
