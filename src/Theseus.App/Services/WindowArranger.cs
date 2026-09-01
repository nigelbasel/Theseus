using System.Runtime.InteropServices;
using Theseus.Core.Diagnostics;
using Theseus.Core.Layout;

namespace Theseus.App.Services;

/// <summary>
/// Puts windows where a plan says they should go, and remembers where they were.
///
/// Remembering is not a nicety. Tiling destroys whatever arrangement somebody had made by hand, and
/// an arrangement that cannot be undone is one nobody will try twice. The positions are captured
/// before the first move and kept until they are put back.
/// </summary>
public sealed class WindowArranger
{
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;
    private const int Restore = 9;

    private readonly Dictionary<nint, LayoutRect> _before = [];

    /// <summary>Whether there is an arrangement to undo.</summary>
    public bool CanRestore => _before.Count > 0;

    /// <summary>
    /// Moves each window to its rectangle. A minimised window is restored first: asking for it to be
    /// arranged is asking to see it.
    /// </summary>
    /// <returns>How many windows were moved, and how many refused.</returns>
    public (int Moved, int Refused) Apply(IReadOnlyList<(TerminalWindow Window, LayoutRect To)> placements)
    {
        ArgumentNullException.ThrowIfNull(placements);

        var moved = 0;
        var refused = 0;

        foreach (var (window, to) in placements)
        {
            _before.TryAdd(window.Handle, window.Bounds);

            if (window.IsMinimised)
            {
                ShowWindow(window.Handle, Restore);
            }

            if (Move(window.Handle, to))
            {
                moved++;
            }
            else
            {
                refused++;
            }
        }

        TheseusLog.Information("Arranged {Moved} window(s); {Refused} would not move", moved, refused);

        return (moved, refused);
    }

    /// <summary>Puts every window back where it was before the first arrangement, and forgets.</summary>
    /// <returns>How many were put back.</returns>
    public int RestoreAll()
    {
        var restored = _before.Count(placement => Move(placement.Key, placement.Value));

        TheseusLog.Information("Put {Count} window(s) back where they were", restored);
        _before.Clear();

        return restored;
    }

    /// <summary>Forgets the remembered positions without moving anything.</summary>
    public void Forget() => _before.Clear();

    private static bool Move(nint window, LayoutRect to)
    {
        if (SetWindowPos(window, nint.Zero, to.X, to.Y, to.Width, to.Height, NoZOrder | NoActivate))
        {
            return true;
        }

        // The number matters. A window that has closed, one owned by a more privileged process and
        // one refusing for a reason nobody has thought of all look identical from here.
        TheseusLog.Warning("Window {Window} would not move to {Width}x{Height} at {X},{Y}: error {Error}",
            window, to.Width, to.Height, to.X, to.Y, Marshal.GetLastWin32Error());

        return false;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(nint window, nint after, int x, int y,
        int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint window, int command);
}
