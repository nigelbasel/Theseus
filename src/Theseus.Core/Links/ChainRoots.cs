namespace Theseus.Core.Links;

/// <summary>
/// Which chain each linked card belongs to, named by the card the chain starts from: the one at the
/// top of its hand-overs. A card with no hand-over link is in no chain and is left out.
/// </summary>
public static class ChainRoots
{
    /// <summary>Every card in a chain, mapped to the key of the card its chain starts from.</summary>
    public static IReadOnlyDictionary<string, string> Of(IEnumerable<Link> links)
    {
        ArgumentNullException.ThrowIfNull(links);

        var handOvers = links.Where(link => link.IsActive && link.Kind == LinkKind.HandsOver).ToList();
        var parentOf = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var link in handOvers)
        {
            parentOf.TryAdd(link.To, link.From);
        }

        var roots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var key in handOvers.SelectMany(link => new[] { link.From, link.To }))
        {
            if (roots.ContainsKey(key))
            {
                continue;
            }

            var root = key;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };

            while (parentOf.TryGetValue(root, out var parent) && seen.Add(parent))
            {
                root = parent;
            }

            roots[key] = root;
        }

        return roots;
    }
}
