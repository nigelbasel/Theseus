namespace Theseus.Core.Domain;

/// <summary>
/// The board as it stood at the end of the last refresh. Session tools run on pool threads, while a
/// card's lifecycle is worked out on the UI thread, so they read this immutable copy instead of the
/// cards themselves.
/// </summary>
/// <param name="Cards">Every card on the board.</param>
/// <param name="TakenUtc">When the refresh that made it finished.</param>
public sealed record BoardSnapshot(IReadOnlyList<BoardSnapshotCard> Cards, DateTimeOffset TakenUtc)
{
    /// <summary>The board before its first refresh.</summary>
    public static BoardSnapshot Empty { get; } = new([], DateTimeOffset.MinValue);

    /// <summary>The card with this key, if it is on the board.</summary>
    public BoardSnapshotCard? Card(string key) =>
        Cards.FirstOrDefault(card => string.Equals(card.Key, key, StringComparison.OrdinalIgnoreCase));
}
