namespace Theseus.Core.Stopping;

/// <summary>
/// When the close step runs again on its own: just after the earliest waiting session's quiet period ends, and not at
/// all while nothing waits (close-step AC 1 and 2).
/// </summary>
public static class CloseRecheck
{
    /// <summary>How long after quiet ends the check runs, so a timer firing a moment early still finds quiet reached.</summary>
    public static readonly TimeSpan Margin = TimeSpan.FromSeconds(1);

    /// <summary>When to check again, or null when no session is waiting.</summary>
    /// <param name="quietEnds">When each waiting session's quiet period ends.</param>
    public static DateTimeOffset? Next(IEnumerable<DateTimeOffset> quietEnds)
    {
        ArgumentNullException.ThrowIfNull(quietEnds);

        DateTimeOffset? earliest = null;

        foreach (var end in quietEnds)
        {
            if (earliest is null || end < earliest)
            {
                earliest = end;
            }
        }

        return earliest + Margin;
    }
}
