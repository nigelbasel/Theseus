namespace Theseus.Core.Finding;

/// <summary>
/// The board's search and tag filter together (item 15): a card shows when it matches the words and the tags. The tags
/// are those ticked in the Tags list and those typed with <c>#</c>, matched the one way the Any | All toggle says.
/// </summary>
/// <param name="Query">What is typed in the box.</param>
/// <param name="TickedTags">The tags ticked in the Tags list.</param>
/// <param name="Match">Any or All, for the tags.</param>
public sealed record CardFilter(SearchQuery Query, IReadOnlyList<string> TickedTags, TagMatch Match)
{
    /// <summary>No search and no tags: the board as it is.</summary>
    public static CardFilter None { get; } = new(SearchQuery.Empty, [], TagMatch.Any);

    /// <summary>The tags filtered by, ticked or typed, each once.</summary>
    public IReadOnlyList<string> Tags => [.. TickedTags.Concat(Query.Tags).Distinct(StringComparer.Ordinal)];

    /// <summary>Whether it filters at all.</summary>
    public bool IsOn => Query.Words.Count > 0 || Tags.Count > 0;

    /// <summary>Whether the card's tags satisfy the tags filtered by. No tags admits every card.</summary>
    public bool AdmitsTags(IReadOnlyList<string> cardTags)
    {
        ArgumentNullException.ThrowIfNull(cardTags);
        var tags = Tags;

        if (tags.Count == 0)
        {
            return true;
        }

        return Match == TagMatch.All
            ? tags.All(tag => cardTags.Contains(tag, StringComparer.Ordinal))
            : tags.Any(tag => cardTags.Contains(tag, StringComparer.Ordinal));
    }

    /// <summary>Whether the card shows: it matches the words, and its tags satisfy the tag filter.</summary>
    public bool Admits(SearchableCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        return AdmitsTags(card.Tags) && CardSearch.Matches(Query.Words, card);
    }

    /// <summary>
    /// What the board's title line says while it filters (item 17): "Filtered: #bym · 'search' · 3 of 112"; empty when
    /// nothing filters. Several tags are joined by "or" under Any and "+" under All.
    /// </summary>
    public string Describe(int matching, int total)
    {
        if (!IsOn)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        var tags = Tags;

        if (tags.Count > 0)
        {
            parts.Add(string.Join(Match == TagMatch.All ? " + " : " or ", tags.Select(tag => "#" + tag)));
        }

        if (Query.Words.Count > 0)
        {
            parts.Add($"'{string.Join(' ', Query.Words)}'");
        }

        parts.Add($"{matching} of {total}");

        return "Filtered: " + string.Join(" · ", parts);
    }
}
