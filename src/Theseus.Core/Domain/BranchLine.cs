namespace Theseus.Core.Domain;

/// <summary>What a card's branch line shows.</summary>
/// <param name="Text">The line itself, or empty when there is nothing to show.</param>
/// <param name="Tooltip">What hovering it says.</param>
/// <param name="Differs">Whether the card's repos are not all on the same branch.</param>
public sealed record BranchLine(string Text, string Tooltip, bool Differs);
