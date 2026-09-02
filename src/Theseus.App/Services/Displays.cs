using System.Runtime.InteropServices;
using Theseus.Core.Layout;

namespace Theseus.App.Services;

/// <summary>
/// The screens attached right now, as work areas.
///
/// Read through <c>EnumDisplayMonitors</c> rather than the framework's screen list, for the same
/// reason the window arranger does: these coordinates are compared against window rectangles and
/// handed back to window positioning, and Win32 keeps all three in one space whatever this process's
/// DPI awareness turns out to be. A source that scales on its own terms is how a window ends up on
/// the wrong monitor by a factor of one and a half.
/// </summary>
public static class Displays
{
    /// <summary>The usable area of every attached screen, taskbars excluded.</summary>
    public static IReadOnlyList<LayoutRect> WorkAreas()
    {
        var found = new List<LayoutRect>();

        EnumDisplayMonitors(nint.Zero, nint.Zero, (monitor, _, _, _) =>
        {
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };

            if (GetMonitorInfo(monitor, ref info))
            {
                found.Add(new LayoutRect(
                    info.WorkArea.Left,
                    info.WorkArea.Top,
                    info.WorkArea.Right - info.WorkArea.Left,
                    info.WorkArea.Bottom - info.WorkArea.Top));
            }

            return true;
        }, nint.Zero);

        return found;
    }

    private delegate bool MonitorEnumProc(nint monitor, nint context, nint rect, nint data);

    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(nint context, nint clip, MonitorEnumProc callback, nint data);

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
