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
    /// <summary>How much of the desktop this covers.</summary>
    public long Area => (long)Math.Max(0, Width) * Math.Max(0, Height);

    /// <summary>The part of this rectangle that is also in the other one, which may be nothing.</summary>
    public LayoutRect Intersect(LayoutRect other)
    {
        var left = Math.Max(X, other.X);
        var top = Math.Max(Y, other.Y);
        var right = Math.Min(X + Width, other.X + other.Width);
        var bottom = Math.Min(Y + Height, other.Y + other.Height);

        return right <= left || bottom <= top
            ? new LayoutRect(left, top, 0, 0)
            : new LayoutRect(left, top, right - left, bottom - top);
    }

    /// <summary>The rectangle inset on every side, never smaller than nothing.</summary>
    public LayoutRect Inset(int by) => new(
        X + by,
        Y + by,
        Math.Max(0, Width - (by * 2)),
        Math.Max(0, Height - (by * 2)));
}
