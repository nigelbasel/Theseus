namespace Theseus.Core.Finding;

/// <summary>How ticked tags filter the board (item 12): cards with any of them, or only cards with all of them.</summary>
public enum TagMatch
{
    /// <summary>A card with any ticked tag shows.</summary>
    Any,

    /// <summary>Only a card with every ticked tag shows.</summary>
    All,
}
