namespace Theseus.Core.Sessions;

/// <summary>
/// What the taskbar menu of live sessions should hold, and whether that differs from what it holds.
///
/// Committing a jump list is a blocking COM call into the shell, made on the UI thread. It was being
/// made on every change to the sessions list - seven times a refresh with six sessions running,
/// whether or not anything had changed - and on 2026-09-15 a dump of the frozen board had the UI
/// thread stuck inside it. The entries only change when a session starts, ends or is renamed, so
/// that is the only time it needs committing.
/// </summary>
public static class SessionMenu
{
    /// <summary>The entries for the sessions a person could be taken to, in the order given.</summary>
    public static IReadOnlyList<SessionMenuEntry> Entries(IEnumerable<LiveSession> sessions)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        return
        [
            .. sessions
                .Where(session => session.IsAdoptable)
                .Select(session => new SessionMenuEntry(session.SessionId, SessionLabel.For(session), session.Cwd ?? string.Empty)),
        ];
    }

    /// <summary>Whether the menu already shows exactly these entries. Nothing applied yet is never the same.</summary>
    public static bool Unchanged(IReadOnlyList<SessionMenuEntry>? applied, IReadOnlyList<SessionMenuEntry> wanted)
    {
        ArgumentNullException.ThrowIfNull(wanted);

        return applied is not null && applied.SequenceEqual(wanted);
    }
}
