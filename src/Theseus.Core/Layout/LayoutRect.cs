namespace Theseus.Core.Layout;

/// <summary>
/// A rectangle a window should occupy, in the coordinate space the caller is working in.
///
/// Which space that is matters more than it looks: a per-monitor aware process sees physical pixels
/// and an unaware one sees logical, and the two differ by the display's scale factor. A plan
/// computed in one and applied in the other puts every window out by that factor - on this machine,
/// half a screen. This type carries no opinion; the caller must work in one space throughout.
/// </summary>
/// <param name="X">Left edge.</param>
/// <param name="Y">Top edge.</param>
/// <param name="Width">Width in pixels.</param>
/// <param name="Height">Height in pixels.</param>
public readonly record struct LayoutRect(int X, int Y, int Width, int Height)
{
    /// <summary>The rectangle inset on every side, never smaller than nothing.</summary>
    public LayoutRect Inset(int by) => new(
        X + by,
        Y + by,
        Math.Max(0, Width - (by * 2)),
        Math.Max(0, Height - (by * 2)));
}
