namespace Theseus.Core.Domain;

/// <summary>
/// Where a dragged card lands once it has been taken out of the lane it came from.
///
/// The subtlety is entirely within one lane: the position the pointer chose was measured against a
/// lane that still contained the card, and lifting it out shifts everything below up by one. Without
/// allowing for that, a card dragged downwards always lands one place further than it was dropped -
/// and a card dragged to the very end never moves at all.
/// </summary>
public static class LaneInsertion
{
    /// <summary>The index to insert at, given where the pointer chose and where the card came from.</summary>
    /// <param name="chosen">The position the drop landed on, counted before the card was removed.</param>
    /// <param name="cameFrom">Its index in this lane before the drag, or null if it came from another.</param>
    /// <param name="remaining">How many cards are in the lane once it has been taken out.</param>
    public static int At(int chosen, int? cameFrom, int remaining)
    {
        var wanted = cameFrom is { } origin && origin < chosen ? chosen - 1 : chosen;

        return Math.Clamp(wanted, 0, remaining);
    }
}
