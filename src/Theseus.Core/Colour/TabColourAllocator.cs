namespace Theseus.Core.Colour;

/// <inheritdoc />
public sealed class TabColourAllocator : ITabColourAllocator
{
    /// <inheritdoc />
    public string Allocate(ColourAllocationState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.InUse.Count == 0)
        {
            return TabColourPalette.All[0];
        }

        var taken = state.InUse.Select(OkLabConverter.FromHex).ToList();

        var free = TabColourPalette.All
            .Where(c => !state.InUse.Any(u => string.Equals(u, c, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (free.Count > 0)
        {
            return FarthestFrom(free, taken);
        }

        var oldestFreed = state.RecentlyFreed
            .Where(r => TabColourPalette.Contains(r.ColourHex))
            .OrderBy(r => r.FreedUtc)
            .ThenBy(r => TabColourPalette.IndexOf(r.ColourHex))
            .FirstOrDefault();

        if (oldestFreed is not null)
        {
            return TabColourPalette.All[TabColourPalette.IndexOf(oldestFreed.ColourHex)];
        }

        return LeastUsed(state.InUse);
    }

    private static string FarthestFrom(IReadOnlyList<string> candidates, IReadOnlyList<OkLabColour> taken)
    {
        var best = candidates[0];
        var bestDistance = double.NegativeInfinity;

        foreach (var candidate in candidates)
        {
            var colour = OkLabConverter.FromHex(candidate);
            var nearest = taken.Min(t => OkLabConverter.Distance(colour, t));

            if (nearest > bestDistance + Tolerance)
            {
                best = candidate;
                bestDistance = nearest;
            }
        }

        return best;
    }

    private static string LeastUsed(IReadOnlyList<string> inUse)
    {
        var best = TabColourPalette.All[0];
        var fewest = int.MaxValue;

        foreach (var colour in TabColourPalette.All)
        {
            var count = inUse.Count(u => string.Equals(u, colour, StringComparison.OrdinalIgnoreCase));

            if (count < fewest)
            {
                fewest = count;
                best = colour;
            }
        }

        return best;
    }

    private const double Tolerance = 1e-9;
}
