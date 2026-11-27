namespace Theseus.Core.Trust;

/// <summary>What keeping a card's folders trusted came to before its launch.</summary>
/// <param name="Trusted">The keys Theseus set now, under the owner's standing consent.</param>
/// <param name="LeftForAClick">Folders the consent does not reach, still asked about in Options.</param>
/// <param name="Failure">Why nothing could be written, or null.</param>
public sealed record StandingTrustOutcome(IReadOnlyList<string> Trusted, IReadOnlyList<FolderTrustNeed> LeftForAClick, string? Failure)
{
    /// <summary>Nothing set and nothing left.</summary>
    public static StandingTrustOutcome Nothing { get; } = new([], [], null);
}
