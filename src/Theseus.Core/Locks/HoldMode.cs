namespace Theseus.Core.Locks;

/// <summary>What keeps a granted lock held.</summary>
public enum HoldMode
{
    /// <summary>
    /// A process holds it: the wrapper around one command. Released when that process exits, however it
    /// exits. Until the wrapper attaches, a granted lock is only reserved, for <see cref="LockBook.Reservation"/>.
    /// </summary>
    Process,

    /// <summary>
    /// A Claude session holds it across many commands. Released when the session releases it, when the
    /// session's process exits, or when Nigel releases it from the board. No timer takes it away.
    /// </summary>
    Session,
}
