namespace Theseus.Core.Links;

/// <summary>
/// An edge between two workstreams. It belongs to neither card, so it lives in its own file rather
/// than on either end. Links are never deleted, only removed, so a removed link still marks its keys
/// as taken and a new card with an old title cannot inherit someone else's history.
/// </summary>
/// <param name="Id">A short random identifier.</param>
/// <param name="Kind">What the link means.</param>
/// <param name="From">The parent of a spawned link, or the upstream of a feeds link.</param>
/// <param name="To">The child of a spawned link, or the downstream of a feeds link.</param>
/// <param name="CreatedUtc">When it was made.</param>
/// <param name="CreatedBy">Who made it: <see cref="MadeOnTheBoard"/> for every link in this phase.</param>
/// <param name="Brief">For a spawned link, the document the child starts from.</param>
/// <param name="HandOver">For a spawned link, the scope that moved from the parent to the child.</param>
/// <param name="Output">For a feeds link, the document the downstream reads.</param>
/// <param name="SatisfiedUtc">When the downstream's input was accepted, or null while it waits.</param>
/// <param name="SatisfiedBy">How it was accepted, so a link satisfied by hand can be told apart later.</param>
/// <param name="RemovedUtc">When it was removed, or null while it stands.</param>
public sealed record Link(
    string Id,
    LinkKind Kind,
    string From,
    string To,
    DateTimeOffset CreatedUtc,
    string CreatedBy,
    string? Brief = null,
    string? HandOver = null,
    string? Output = null,
    DateTimeOffset? SatisfiedUtc = null,
    string? SatisfiedBy = null,
    DateTimeOffset? RemovedUtc = null)
{
    /// <summary>What <see cref="CreatedBy"/> says for a link Nigel made himself.</summary>
    public const string MadeOnTheBoard = "board";

    /// <summary>Whether the link still stands.</summary>
    public bool IsActive => RemovedUtc is null;

    /// <summary>Whether the link joins this card at either end.</summary>
    public bool Touches(string key) =>
        string.Equals(From, key, StringComparison.OrdinalIgnoreCase)
        || string.Equals(To, key, StringComparison.OrdinalIgnoreCase);
}
