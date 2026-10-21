namespace Theseus.Core.Links;

/// <summary>A card's link line, as the card shows it.</summary>
/// <param name="Text">One line, words first; empty for a card with no links.</param>
/// <param name="Tooltip">Every link on the card, one per line.</param>
/// <param name="IsReady">Whether the line leads with Ready, which the card shows in green.</param>
public sealed record LinkLineText(string Text, string Tooltip, bool IsReady)
{
    /// <summary>The line for a card with no links.</summary>
    public static LinkLineText None { get; } = new(string.Empty, string.Empty, false);
}
