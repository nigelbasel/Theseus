using Theseus.Core.Links;

namespace Theseus.App.ViewModels;

/// <summary>One way "Link to…" can join the card being looked at to another, worded from that card.</summary>
/// <param name="Label">What the picker says, completing "This card …".</param>
/// <param name="Kind">The kind of link it makes.</param>
/// <param name="ThisCardIsFrom">Whether this card is the end that writes the link's document.</param>
public sealed record LinkKindChoice(string Label, LinkKind Kind, bool ThisCardIsFrom)
{
    /// <summary>The three ways, in the order the picker offers them.</summary>
    public static IReadOnlyList<LinkKindChoice> All { get; } =
    [
        new("hands over to", LinkKind.HandsOver, ThisCardIsFrom: true),
        new("takes over from", LinkKind.HandsOver, ThisCardIsFrom: false),
        new("reports back to", LinkKind.ReportsBack, ThisCardIsFrom: true),
    ];

    /// <inheritdoc />
    public override string ToString() => Label;
}
