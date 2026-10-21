namespace Theseus.Core.Links;

/// <summary>What a card's inputs say about starting it. Ready only ever informs; it never starts anything.</summary>
public enum CardReadiness
{
    /// <summary>Nothing to say: no inputs, or the card is live, archived, blocked or has run since.</summary>
    None,

    /// <summary>At least one input has not been accepted.</summary>
    Waiting,

    /// <summary>Every input has been accepted and the card has not run since.</summary>
    Ready,
}
