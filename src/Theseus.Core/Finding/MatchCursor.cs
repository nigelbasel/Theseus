namespace Theseus.Core.Finding;

/// <summary>
/// Which match the search keys move to (item 6): Enter selects the first, Enter again or F3 the next, Shift+F3 the one
/// before, wrapping at either end as a find bar does.
/// </summary>
public static class MatchCursor
{
    /// <summary>
    /// The index to move to among <paramref name="count"/> matches from <paramref name="current"/> (-1 for none yet):
    /// the first going forward from none, the last going back from none; -1 when there are no matches.
    /// </summary>
    public static int Step(int count, int current, bool forward)
    {
        if (count <= 0)
        {
            return -1;
        }

        if (current < 0 || current >= count)
        {
            return forward ? 0 : count - 1;
        }

        return forward ? (current + 1) % count : (current - 1 + count) % count;
    }
}
