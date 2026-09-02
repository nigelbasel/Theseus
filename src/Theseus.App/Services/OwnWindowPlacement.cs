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
/// </summary>
public static class OwnWindowPlacement
{
    private const int Restore = 9;
    private const int Maximise = 3;
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;

    /// <summary>Where the window is now, and whether it is maximised.</summary>
    public static WindowPlacement? Capture(nint window)
    {
        if (window == nint.Zero || !GetWindowRect(window, out var rect))
        {
            return null;
        }

        return new WindowPlacement(
            new LayoutRect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top),
            IsZoomed(window));
    }

    /// <summary>
    /// Puts the window where the placement says. A maximised window is restored first, so the size
    /// it would return to is the remembered one rather than whatever it happened to have.
    /// </summary>
    public static void Apply(nint window, WindowPlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);

        if (window == nint.Zero)
        {
            return;
        }

        ShowWindow(window, Restore);

        var bounds = placement.Bounds;

        SetWindowPos(window, nint.Zero, bounds.X, bounds.Y, bounds.Width, bounds.Height,
            NoZOrder | NoActivate);

        if (placement.IsMaximised)
        {
            ShowWindow(window, Maximise);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint window, out Rect rect);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(nint window);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y,
        int width, int height, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
