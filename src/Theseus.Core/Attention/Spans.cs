namespace Theseus.Core.Attention;

/// <summary>Arithmetic on stretches of time, for taking one set out of another (7.3: ledger overlaps, lock waits).</summary>
public static class Spans
{
    /// <summary>The parts of <paramref name="span"/> that none of <paramref name="removed"/> covers, in order.</summary>
    public static IReadOnlyList<(DateTimeOffset Start, DateTimeOffset End)> Without(
        (DateTimeOffset Start, DateTimeOffset End) span, IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> removed)
    {
        ArgumentNullException.ThrowIfNull(removed);

        var left = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        var cursor = span.Start;

        foreach (var cut in removed.Where(cut => cut.End > span.Start && cut.Start < span.End).OrderBy(cut => cut.Start))
        {
            if (cut.Start > cursor)
            {
                left.Add((cursor, cut.Start));
            }

            if (cut.End > cursor)
            {
                cursor = cut.End;
            }
        }

        if (span.End > cursor)
        {
            left.Add((cursor, span.End));
        }

        return left;
    }

    /// <summary>How much of <paramref name="span"/> the union of <paramref name="others"/> covers.</summary>
    public static TimeSpan Covered((DateTimeOffset Start, DateTimeOffset End) span, IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> others)
    {
        var length = span.End > span.Start ? span.End - span.Start : TimeSpan.Zero;

        return length - Without(span, others).Aggregate(TimeSpan.Zero, (sum, part) => sum + (part.End - part.Start));
    }
}
