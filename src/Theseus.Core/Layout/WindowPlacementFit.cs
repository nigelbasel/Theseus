namespace Theseus.Core.Layout;

/// <summary>
/// Makes a remembered window position safe to use on whatever screens are attached now.
///
/// A saved position is a fact about a desktop that may no longer exist. Undock a laptop and the
/// second monitor's coordinates become empty space; dock it again on the other side and they move.
/// An application that restores its position without asking this question opens somewhere nobody can
/// see, and the only way back is to know about arrow-key window moving or to edit a settings file.
/// Several do exactly that, which is why this was asked for.
///
/// So the saved rectangle is treated as a wish rather than an instruction. If enough of it lands on
/// a screen that is actually there, it is honoured. If not, it is brought back onto the nearest
/// screen rather than discarded: somebody who made the window small and put it on the right probably
/// still wants it small and on the right.
/// </summary>
public static class WindowPlacementFit
{
    /// <summary>
    /// How much of a window must be on a screen for it to count as reachable, as a fraction of its
    /// own area. A quarter is generous - the window is plainly visible - and still catches the case
    /// where a monitor has gone and only a corner remains.
    /// </summary>
    public const double MinimumVisible = 0.25;

    /// <summary>
    /// The strip of title bar that must be reachable, in pixels. A window can be recovered by
    /// dragging it, but only if there is something to take hold of.
    /// </summary>
    public const int GrabWidth = 120;

    /// <summary>How far down the window the title bar is taken to reach.</summary>
    public const int GrabHeight = 32;

    /// <summary>
    /// The placement to use now: the saved one where it still works, a corrected one where it does
    /// not, and null when there is nothing to correct it against and the caller should use its own
    /// defaults.
    /// </summary>
    /// <param name="placement">What was saved, or null if nothing was.</param>
    /// <param name="screens">The work areas of the screens attached now.</param>
    public static WindowPlacement? Onto(WindowPlacement? placement, IReadOnlyList<LayoutRect> screens)
    {
        ArgumentNullException.ThrowIfNull(screens);

        if (placement is null || screens.Count == 0 || placement.Bounds.Area <= 0)
        {
            return null;
        }

        return IsReachable(placement.Bounds, screens)
            ? placement
            : placement with { Bounds = Rescue(placement.Bounds, Nearest(placement.Bounds, screens)) };
    }

    /// <summary>Whether a window at these bounds could be seen and taken hold of.</summary>
    public static bool IsReachable(LayoutRect bounds, IReadOnlyList<LayoutRect> screens)
    {
        ArgumentNullException.ThrowIfNull(screens);

        if (bounds.Area <= 0)
        {
            return false;
        }

        var visible = screens.Sum(screen => bounds.Intersect(screen).Area);

        if (visible < bounds.Area * MinimumVisible)
        {
            return false;
        }

        // Area alone is not enough. A window can be three-quarters on screen with its title bar
        // above the top edge, and then it cannot be moved with a mouse at all.
        var titleBar = new LayoutRect(bounds.X, bounds.Y, bounds.Width, Math.Min(GrabHeight, bounds.Height));

        return screens.Any(screen => titleBar.Intersect(screen) is { Height: > 0 } grab
                                     && grab.Width >= Math.Min(GrabWidth, bounds.Width));
    }

    private static LayoutRect Nearest(LayoutRect bounds, IReadOnlyList<LayoutRect> screens) =>
        screens.OrderByDescending(screen => bounds.Intersect(screen).Area).First();

    /// <summary>
    /// The window brought back onto one screen, keeping as much of its size as that screen allows.
    /// A window saved on a 4K monitor and restored on a laptop is shrunk to fit rather than left
    /// hanging off two edges.
    /// </summary>
    private static LayoutRect Rescue(LayoutRect bounds, LayoutRect screen)
    {
        var width = Math.Min(bounds.Width, screen.Width);
        var height = Math.Min(bounds.Height, screen.Height);

        return new LayoutRect(
            Math.Clamp(bounds.X, screen.X, screen.X + screen.Width - width),
            Math.Clamp(bounds.Y, screen.Y, screen.Y + screen.Height - height),
            width,
            height);
    }
}
