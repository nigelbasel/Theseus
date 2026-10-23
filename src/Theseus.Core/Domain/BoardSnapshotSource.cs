namespace Theseus.Core.Domain;

/// <summary>
/// Where the board publishes its snapshot at the end of each refresh and session tools pick it up.
/// The swap is a single reference exchange, so a reader sees one whole snapshot or the one before.
/// </summary>
public sealed class BoardSnapshotSource
{
    private BoardSnapshot _current = BoardSnapshot.Empty;

    /// <summary>The newest snapshot.</summary>
    public BoardSnapshot Current => Volatile.Read(ref _current);

    /// <summary>Replaces the snapshot.</summary>
    public void Publish(BoardSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Interlocked.Exchange(ref _current, snapshot);
    }
}
