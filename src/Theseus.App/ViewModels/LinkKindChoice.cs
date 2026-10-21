using Theseus.Core.Links;

namespace Theseus.App.ViewModels;

/// <summary>One way "Link to…" can join the card being looked at to another, worded from that card.</summary>
/// <param name="Label">What the picker says, completing "This card …".</param>
/// <param name="Kind">The kind of link it makes.</param>
/// <param name="ThisCardIsFrom">Whether this card is the link's parent or upstream end.</param>
public sealed record LinkKindChoice(string Label, LinkKind Kind, bool ThisCardIsFrom)
{
    /// <summary>The four ways, in the order the picker offers them.</summary>
    public static IReadOnlyList<LinkKindChoice> All { get; } =
    [
        new("waits on the output of", LinkKind.Feeds, ThisCardIsFrom: false),
        new("feeds its output to", LinkKind.Feeds, ThisCardIsFrom: true),
        new("was spawned by", LinkKind.Spawned, ThisCardIsFrom: false),
        new("spawned", LinkKind.Spawned, ThisCardIsFrom: true),
    ];

    /// <inheritdoc />
    public override string ToString() => Label;
}
