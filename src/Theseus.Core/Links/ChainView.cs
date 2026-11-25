using Theseus.Core.Finding;

namespace Theseus.Core.Links;

/// <summary>
/// The Chains panel's search and filters over its tree (chains-panel-at-scale criteria 3, 8, 9 and 15). A row is kept when
/// the search matches it and every filter keeps it; a row that is not kept stays as context while anything below it is.
/// Hide completed works on whole chains: a finished card in an unfinished chain stays, so the chain still reads whole.
/// </summary>
public static class ChainView
{
    private static readonly ChainFilters NoFilters = new(false, false, false);

    /// <summary>What shows for the search and filters given, and what each filter would hide on its own.</summary>
    public static ChainViewResult Apply(IReadOnlyList<ChainEntry> roots, SearchQuery query, ChainFilters filters)
    {
        ArgumentNullException.ThrowIfNull(roots);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(filters);

        var search = new CardFilter(query, [], TagMatch.Any);
        var visibility = Visibilities(roots, filters, entry => search.Admits(entry.Card));

        return new ChainViewResult(
            visibility,
            visibility.Values.Count(value => value != ChainVisibility.Hidden),
            visibility.Count,
            HiddenAlone(roots, NoFilters with { ActiveOnly = true }),
            HiddenAlone(roots, NoFilters with { WaitingOnMe = true }),
            HiddenAlone(roots, NoFilters with { HideCompleted = true }),
            query.IsEmpty ? [] : [.. visibility.Where(pair => pair.Value == ChainVisibility.Context).Select(pair => pair.Key)]);
    }

    /// <summary>Whether a row has cards below it and every one of them is completed, so it starts folded (criterion 15).</summary>
    public static bool IsFinishedBelow(ChainEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return entry.Children.Count > 0 && AllCompletedBelow(entry);
    }

    private static bool AllCompletedBelow(ChainEntry entry) =>
        entry.Children.All(child => ChainRowStates.IsCompleted(child.Facts) && AllCompletedBelow(child));

    private static int HiddenAlone(IReadOnlyList<ChainEntry> roots, ChainFilters filters) =>
        Visibilities(roots, filters, _ => true).Values.Count(value => value == ChainVisibility.Hidden);

    private static Dictionary<string, ChainVisibility> Visibilities(IReadOnlyList<ChainEntry> roots, ChainFilters filters,
        Func<ChainEntry, bool> matches)
    {
        var result = new Dictionary<string, ChainVisibility>(StringComparer.OrdinalIgnoreCase);

        foreach (var root in roots)
        {
            var chainHidden = filters.HideCompleted && ChainRowStates.IsCompleted(root.Facts) && AllCompletedBelow(root);
            Visit(root, chainHidden, filters, matches, result);
        }

        return result;
    }

    // Whether this row or any below it is kept, recording each row's visibility on the way back up.
    private static bool Visit(ChainEntry entry, bool chainHidden, ChainFilters filters, Func<ChainEntry, bool> matches,
        Dictionary<string, ChainVisibility> result)
    {
        var keptBelow = false;

        foreach (var child in entry.Children)
        {
            keptBelow |= Visit(child, chainHidden, filters, matches, result);
        }

        var kept = !chainHidden
                   && (!filters.ActiveOnly || ChainRowStates.IsActive(entry.Facts))
                   && (!filters.WaitingOnMe || ChainRowStates.WaitsOnOwner(entry.Facts))
                   && matches(entry);

        result[entry.Key] = kept ? ChainVisibility.Shown : keptBelow ? ChainVisibility.Context : ChainVisibility.Hidden;

        return kept || keptBelow;
    }
}
