namespace Theseus.Core.Stopping;

/// <summary>
/// When Theseus tries again after a card's turn ended on a transport error (2.4 W2): a minute after the error, then twice
/// as long after each further error, up to <see cref="Cap"/> tries in a row. A DNS outage on 2026-11-25 lasted about half
/// an hour, which six tries over an hour cover; a machine that slept is tried as soon as it wakes, since the first delay
/// has long passed. After the cap the card waits for the owner, as it did before.
/// </summary>
public static class TransportResume
{
    /// <summary>How many tries in a row, before the card is left for the owner.</summary>
    public const int Cap = 6;

    /// <summary>What the card is told.</summary>
    public const string Text =
        "Carry on where you were: your last turn ended on a network or API error, not by your choice. Check that what you "
        + "were running finished, and go on from there.";

    private static readonly TimeSpan First = TimeSpan.FromMinutes(1);

    /// <summary>
    /// When to try again after the error a card's turn ended on, or null: not again for the error last tried for, whose try
    /// has not ended on another yet, and not after the cap.
    /// </summary>
    /// <param name="errorUtc">When the turn ended on the error.</param>
    /// <param name="streak">The error last tried for and the tries in a row so far, or null for none.</param>
    public static DateTimeOffset? NextTryUtc(DateTimeOffset errorUtc, (DateTimeOffset TriedForUtc, int Tries)? streak) =>
        streak is { } tried && errorUtc <= tried.TriedForUtc ? null : DueUtc(errorUtc, streak?.Tries ?? 0);

    /// <summary>When the next try is due, or null once <paramref name="triesSoFar"/> has reached the cap.</summary>
    /// <param name="errorUtc">When the turn ended on the error.</param>
    /// <param name="triesSoFar">How many tries were made since the card last ended a turn without one.</param>
    public static DateTimeOffset? DueUtc(DateTimeOffset errorUtc, int triesSoFar) =>
        triesSoFar < 0 || triesSoFar >= Cap ? null : errorUtc + (First * Math.Pow(2, triesSoFar));
}
