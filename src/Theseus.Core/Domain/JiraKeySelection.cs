namespace Theseus.Core.Domain;

/// <summary>
/// Which Jira issues a card shows, from the three places they come from.
///
/// A workstream's issues arrive from its opening prompt, from the session that is working on it -
/// which is the only party that knows about an issue raised an hour ago - and from the board, where
/// they can be typed in and taken away by hand. None of the three is authoritative on its own: a
/// session cannot know what was added by hand, and the board cannot know what the session has just
/// picked up.
///
/// So they are combined rather than ranked, with one exception. Taking a key off a card has to mean
/// something permanent, or a key the session mentions once would come back every time the board was
/// read, and there would be no way to be rid of it. Removals are therefore remembered, and a key
/// removed stays removed until it is added again on purpose.
/// </summary>
public static class JiraKeySelection
{
    /// <summary>Every key a card should show.</summary>
    /// <param name="declared">What the session recorded in its plan file.</param>
    /// <param name="prompt">The opening prompt, which usually names the issue the work began from.</param>
    /// <param name="kept">What the board was last told to show.</param>
    /// <param name="removed">What was taken off the card by hand.</param>
    public static IReadOnlyList<string> Visible(
        IEnumerable<string>? declared,
        string? prompt,
        IEnumerable<string>? kept,
        IEnumerable<string>? removed)
    {
        var gone = new HashSet<string>(removed ?? [], StringComparer.OrdinalIgnoreCase);

        return JiraKeyExtractor.Extract(prompt)
            .Concat(Normalised(declared))
            .Concat(Normalised(kept))
            .Where(key => !gone.Contains(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// What to remember as removed, after the card has been edited. A key that was on the card and
    /// is no longer joins the list; one that has been put back leaves it, so adding a key by hand
    /// undoes having taken it away without anybody having to know that is what they are doing.
    /// </summary>
    public static IReadOnlyList<string> Removed(
        IEnumerable<string>? wasVisible,
        IEnumerable<string>? nowVisible,
        IEnumerable<string>? alreadyRemoved)
    {
        var showing = new HashSet<string>(Normalised(nowVisible), StringComparer.OrdinalIgnoreCase);

        return Normalised(alreadyRemoved)
            .Concat(Normalised(wasVisible))
            .Where(key => !showing.Contains(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<string> Normalised(IEnumerable<string>? keys) =>
        (keys ?? []).Select(JiraKeyExtractor.Normalise).OfType<string>();
}
