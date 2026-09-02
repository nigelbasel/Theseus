using System.Runtime.InteropServices;
using Theseus.Core.Layout;

namespace Theseus.App.Services;

/// <summary>
/// Reads and writes the board window's own position, in Win32 terms throughout.
///
/// WPF measures <c>Left</c>, <c>Top</c>, <c>Width</c> and <c>Height</c> in device-independent units,
/// and the screens this position is checked against are measured in physical pixels. On this machine
/// those differ by half again. Setting a position read from one in the units of the other is how a
/// window lands on the wrong monitor, so the whole round trip stays on one side of that line.
///
/// It uses <c>GetWindowPlacement</c> rather than <c>GetWindowRect</c>, which matters for a maximised
/// window and was got wrong the first time. A maximised window's rectangle *is* the screen, so
/// remembering it remembers nothing: the window came back maximised and its restore button had only
/// the full screen to return to. Placement carries both facts separately — whether it was maximised,
/// and the size it would go back to — which is exactly the pair that has to survive.
/// </summary>
public static class OwnWindowPlacement
{
    private const int Normal = 1;
    private const int Maximised = 3;
    private const int Minimised = 2;

    /// <summary>Where the window is now, and whether it is maximised.</summary>
    public static WindowPlacement? Capture(nint window)
    {
        if (window == nint.Zero)
        {
            return null;
        }

        var placement = new Placement { Length = Marshal.SizeOf<Placement>() };

        if (!GetWindowPlacement(window, ref placement))
        {
            return null;
        }

        var normal = placement.NormalPosition;

        return new WindowPlacement(
            new LayoutRect(
                normal.Left,
                normal.Top,
                normal.Right - normal.Left,
                normal.Bottom - normal.Top),
            placement.ShowCommand == Maximised);
    }

    /// <summary>
    /// Puts the window where the placement says, maximised or not, with the size it would return to
    /// set either way. A window restored from maximised then has somewhere to go back to.
    ///
    /// A window saved while minimised is opened normally rather than minimised: an application that
    /// starts into the taskbar looks like one that failed to start.
    /// </summary>
    public static void Apply(nint window, WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        if (window == nint.Zero)
        {
            return;
        }

        var bounds = placement.Bounds;

        var wanted = new Placement
        {
            Length = Marshal.SizeOf<Placement>(),
            ShowCommand = placement.IsMaximised ? Maximised : Normal,
            NormalPosition = new Rect
            {
                Left = bounds.X,
                Top = bounds.Y,
                Right = bounds.X + bounds.Width,
                Bottom = bounds.Y + bounds.Height,
            },
        };

        SetWindowPlacement(window, ref wanted);
    }

    /// <summary>Whether a window is minimised, which is not a position worth remembering.</summary>
    public static bool IsMinimised(nint window)
    {
        var placement = new Placement { Length = Marshal.SizeOf<Placement>() };

        return window != nint.Zero
               && GetWindowPlacement(window, ref placement)
               && placement.ShowCommand == Minimised;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowPlacement(nint window, ref Placement placement);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPlacement(nint window, ref Placement placement);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Placement
    {
        public int Length;
        public int Flags;
        public int ShowCommand;
        public Point MinimisedPosition;
        public Point MaximisedPosition;
        public Rect NormalPosition;
    }
}
