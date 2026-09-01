namespace Theseus.Core.Terminal;

/// <summary>
/// The order to try terminal windows in when looking for the one holding a session.
///
/// A window has to be found by what it contains, because a process identifies nothing: one
/// <c>WindowsTerminal.exe</c> hosts every window, so asking which window belongs to a session's
/// terminal process returns an arbitrary one of them. Reading tabs is the reliable answer and it is
/// not free, so the window the process points at is tried first — it is right whenever there is only
/// one, which is most of the time, and the search costs nothing in that case.
/// </summary>
public static class WindowSearchOrder
{
    /// <summary>
    /// Every window, with the likeliest first and nothing tried twice.
    /// </summary>
    /// <param name="likeliest">The window the session's process points at, if any.</param>
    /// <param name="all">Every terminal window on the desktop.</param>
    public static IReadOnlyList<nint> PreferringFirst(nint? likeliest, IReadOnlyList<nint> all)
    {
        ArgumentNullException.ThrowIfNull(all);

        if (likeliest is not { } first || !all.Contains(first))
        {
            return all;
        }

        return [first, .. all.Where(window => window != first)];
    }
}
