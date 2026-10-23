namespace Theseus.Core.Links;

/// <summary>
/// An edge between two workstreams. It belongs to neither card, so it lives in its own file rather
/// than on either end. Links are never deleted, only removed, so a removed link still marks its keys
/// as taken and a new card with an old title cannot inherit someone else's history.
/// </summary>
/// <param name="Id">A short random identifier.</param>
/// <param name="Kind">What the link means.</param>
/// <param name="From">The card that hands over, or the child that reports back.</param>
/// <param name="To">The card handed over to, or the parent reported back to.</param>
/// <param name="CreatedUtc">When it was made.</param>
/// <param name="CreatedBy">Who made it: <see cref="MadeOnTheBoard"/> for a link Nigel made.</param>
/// <param name="Output">The document <paramref name="From"/> writes and <paramref name="To"/> reads, or null when the link names none.</param>
/// <param name="SatisfiedUtc">When Nigel accepted the document, or null while it waits.</param>
/// <param name="SatisfiedBy">How it was accepted, so a link satisfied by hand can be told apart later.</param>
/// <param name="RemovedUtc">When it was removed, or null while it stands.</param>
/// <param name="LatestReport">What <paramref name="From"/> last reported, or null.</param>
public sealed record Link(
    string Id,
    LinkKind Kind,
    string From,
    string To,
    DateTimeOffset CreatedUtc,
    string CreatedBy,
    string? Output = null,
    DateTimeOffset? SatisfiedUtc = null,
    string? SatisfiedBy = null,
    DateTimeOffset? RemovedUtc = null,
    LinkReport? LatestReport = null)
{
    /// <summary>What <see cref="CreatedBy"/> says for a link Nigel made himself.</summary>
    public const string MadeOnTheBoard = "board";

    /// <summary>Whether the link still stands.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsActive => RemovedUtc is null;

    /// <summary>Whether the link joins this card at either end.</summary>
    public bool Touches(string key) =>
        string.Equals(From, key, StringComparison.OrdinalIgnoreCase)
        || string.Equals(To, key, StringComparison.OrdinalIgnoreCase);
}
