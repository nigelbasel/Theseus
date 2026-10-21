namespace Theseus.Core.Links;

/// <summary>
/// The links read against the board as it stands right now. Only events are stored (made, satisfied,
/// removed); every state here is computed from them and from the cards.
/// </summary>
public sealed class LinkStatus
{
    private readonly IReadOnlyList<Link> _links;
    private readonly Dictionary<string, LinkCard> _cards;

    /// <summary>Reads these links against these cards. Removed links are ignored.</summary>
    public LinkStatus(IReadOnlyList<Link> links, IEnumerable<LinkCard> cards)
    {
        _links = [.. links.Where(link => link.IsActive)];
        _cards = cards.ToDictionary(card => card.Key, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>The links that still stand.</summary>
    public IReadOnlyList<Link> Links => _links;

    /// <summary>The card with this key, if it is on the board.</summary>
    public LinkCard? Card(string key) => _cards.GetValueOrDefault(key);

    /// <summary>Where one link stands.</summary>
    public LinkState StateOf(Link link)
    {
        if (!_cards.ContainsKey(link.From) || !_cards.ContainsKey(link.To))
        {
            return LinkState.CardMissing;
        }

        if (link.Kind == LinkKind.Spawned)
        {
            return LinkState.Lineage;
        }

        return link.SatisfiedUtc is null ? LinkState.Waiting : LinkState.Satisfied;
    }

    /// <summary>
    /// Whether a card waits on its inputs or is ready to start. A card that has run since its inputs
    /// were last accepted is neither, or it would show Ready for ever.
    /// </summary>
    public CardReadiness ReadinessOf(string key)
    {
        if (_cards.GetValueOrDefault(key) is not { } card || card.IsLive)
        {
            return CardReadiness.None;
        }

        var inputs = Inputs(key);

        if (inputs.Count == 0)
        {
            return CardReadiness.None;
        }

        if (inputs.Any(link => link.SatisfiedUtc is null))
        {
            return CardReadiness.Waiting;
        }

        if (card.IsArchived || card.IsBlocked)
        {
            return CardReadiness.None;
        }

        var accepted = inputs.Max(link => link.SatisfiedUtc!.Value);

        return card.LastRunStartedUtc is { } lastRun && lastRun >= accepted
            ? CardReadiness.None
            : CardReadiness.Ready;
    }

    /// <summary>The upstream keys a card still waits on.</summary>
    public IReadOnlyList<string> WaitingOn(string key) =>
        [.. Inputs(key).Where(link => link.SatisfiedUtc is null).Select(link => link.From)];

    /// <summary>The feeds links into a card.</summary>
    public IReadOnlyList<Link> Inputs(string key) =>
        [.. _links.Where(link => link.Kind == LinkKind.Feeds && Same(link.To, key))];

    /// <summary>Every link touching a card.</summary>
    public IReadOnlyList<Link> For(string key) => [.. _links.Where(link => link.Touches(key))];

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
