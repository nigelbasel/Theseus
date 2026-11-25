namespace Theseus.Core.Finding;

/// <summary>
/// The tags' own small palette (card-search-and-tags item 11): a tag takes the least-used colour on first use, and can be
/// given another in Options, Tags. Mid-tone colours, readable as an outline and a light wash in both themes.
/// </summary>
public static class TagPalette
{
    /// <summary>The colours a tag can have, as <c>#rrggbb</c>.</summary>
    public static IReadOnlyList<string> Colours { get; } =
    [
        "#3B82C4", "#2E9E6E", "#C2852B", "#B4508F", "#7B61C9", "#C25B4A", "#3A9AA5", "#7A8A3A",
    ];

    /// <summary>The colour for a new tag: the palette's least used among those given, the earliest on a tie.</summary>
    public static string Next(IEnumerable<string> used)
    {
        ArgumentNullException.ThrowIfNull(used);
        var counts = used.GroupBy(colour => colour, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return Colours.OrderBy(colour => counts.GetValueOrDefault(colour)).First();
    }

    /// <summary>
    /// The colours with every tag given one: those already set kept, the rest taking <see cref="Next"/> in turn. Returns
    /// the same dictionary when nothing was missing, so a caller can tell whether to write.
    /// </summary>
    public static IReadOnlyDictionary<string, string> Assign(IReadOnlyDictionary<string, string> colours, IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(colours);
        ArgumentNullException.ThrowIfNull(tags);
        var missing = tags.Where(tag => !colours.ContainsKey(tag)).Distinct(StringComparer.Ordinal).ToList();

        if (missing.Count == 0)
        {
            return colours;
        }

        var assigned = new Dictionary<string, string>(colours, StringComparer.Ordinal);

        foreach (var tag in missing)
        {
            assigned[tag] = Next(assigned.Values);
        }

        return assigned;
    }
}
