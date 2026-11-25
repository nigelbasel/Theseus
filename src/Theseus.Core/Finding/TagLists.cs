namespace Theseus.Core.Finding;

/// <summary>
/// Changes to one card's tags and counts across cards (card-search-and-tags items 10 and 13): adding and removing keep
/// the order the owner gave them, renaming onto a tag the card already has merges the two.
/// </summary>
public static class TagLists
{
    /// <summary>The tags with this one added at the end, if not there already.</summary>
    public static IReadOnlyList<string> With(IReadOnlyList<string> tags, string tag)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return tags.Contains(tag, StringComparer.Ordinal) ? tags : [.. tags, tag];
    }

    /// <summary>The tags without this one.</summary>
    public static IReadOnlyList<string> Without(IReadOnlyList<string> tags, string tag)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return [.. tags.Where(existing => !string.Equals(existing, tag, StringComparison.Ordinal))];
    }

    /// <summary>The tags with <paramref name="from"/> renamed <paramref name="to"/> in its place, kept once.</summary>
    public static IReadOnlyList<string> Renamed(IReadOnlyList<string> tags, string from, string to)
    {
        ArgumentNullException.ThrowIfNull(tags);

        return [.. tags.Select(tag => string.Equals(tag, from, StringComparison.Ordinal) ? to : tag).Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Every tag carried by the cards given, with how many carry it, in alphabetical order.</summary>
    public static IReadOnlyList<TagCount> Counts(IEnumerable<IReadOnlyList<string>> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        return [.. cards.SelectMany(tags => tags.Distinct(StringComparer.Ordinal))
            .GroupBy(tag => tag, StringComparer.Ordinal)
            .Select(group => new TagCount(group.Key, group.Count()))
            .OrderBy(count => count.Tag, StringComparer.Ordinal)];
    }
}
