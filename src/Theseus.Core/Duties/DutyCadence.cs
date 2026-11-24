namespace Theseus.Core.Duties;

/// <summary>
/// When a duty is due: every interval from its anchor, or at set local times. Due times are a grid, never a count of runs,
/// so a run that was late or missed does not shift the ones after it.
/// </summary>
/// <param name="Kind">Every or At.</param>
/// <param name="Interval">For Every: the spacing, at least <see cref="ShortestInterval"/>.</param>
/// <param name="Times">For At: the local times of day.</param>
/// <param name="Days">For At: the weekdays it runs on; empty means every day.</param>
public sealed record DutyCadence(DutyCadenceKind Kind, TimeSpan Interval, IReadOnlyList<TimeOnly> Times, IReadOnlyList<DayOfWeek> Days)
{
    /// <summary>The shortest interval a duty may have.</summary>
    public static readonly TimeSpan ShortestInterval = TimeSpan.FromMinutes(5);

    /// <summary>A cadence of every <paramref name="interval"/>.</summary>
    public static DutyCadence Every(TimeSpan interval) => new(DutyCadenceKind.Every, interval, [], []);

    /// <summary>A cadence at set times, on the given days or every day.</summary>
    public static DutyCadence At(IReadOnlyList<TimeOnly> times, IReadOnlyList<DayOfWeek>? days = null) =>
        new(DutyCadenceKind.At, TimeSpan.Zero, [.. times.Distinct().Order()], [.. (days ?? []).Distinct()]);

    /// <summary>Equal by value, lists included, so an edit that keeps the cadence does not move the duty's grid.</summary>
    public bool Equals(DutyCadence? other) =>
        other is not null && Kind == other.Kind && Interval == other.Interval
        && Times.SequenceEqual(other.Times) && Days.Order().SequenceEqual(other.Days.Order());

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Kind, Interval, Times.Count, Days.Count);

    /// <summary>Why this cadence cannot be used, or null when it can.</summary>
    public string? Refusal() => Kind switch
    {
        DutyCadenceKind.Every when Interval < ShortestInterval => $"A duty runs at most every {ShortestInterval.TotalMinutes:0} minutes.",
        DutyCadenceKind.At when Times.Count == 0 => "Give at least one time of day.",
        _ => null,
    };

    /// <summary>The first due time strictly after <paramref name="afterUtc"/>.</summary>
    /// <param name="anchorUtc">For Every: when the duty was set, which the grid counts from.</param>
    /// <param name="afterUtc">The moment to look after.</param>
    /// <param name="zone">The local time zone set times are in.</param>
    public DateTimeOffset NextAfter(DateTimeOffset anchorUtc, DateTimeOffset afterUtc, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (Kind == DutyCadenceKind.Every)
        {
            if (afterUtc < anchorUtc)
            {
                return anchorUtc + Interval;
            }

            var steps = (long)Math.Floor((afterUtc - anchorUtc).Ticks / (double)Interval.Ticks) + 1;

            return anchorUtc + TimeSpan.FromTicks(Interval.Ticks * steps);
        }

        var local = TimeZoneInfo.ConvertTime(afterUtc, zone);

        for (var day = 0; day <= 7; day++)
        {
            var date = DateOnly.FromDateTime(local.Date).AddDays(day);

            if (Days.Count > 0 && !Days.Contains(date.DayOfWeek))
            {
                continue;
            }

            foreach (var time in Times)
            {
                var wall = date.ToDateTime(time);
                var candidate = new DateTimeOffset(wall, zone.GetUtcOffset(wall));

                if (candidate > afterUtc)
                {
                    return candidate.ToUniversalTime();
                }
            }
        }

        return afterUtc + TimeSpan.FromDays(7);
    }

    /// <summary>How many due times fall in (<paramref name="fromUtc"/>, <paramref name="toUtc"/>], at most <paramref name="limit"/>.</summary>
    public int CountBetween(DateTimeOffset anchorUtc, DateTimeOffset fromUtc, DateTimeOffset toUtc, TimeZoneInfo zone, int limit = 1000)
    {
        var count = 0;
        var at = fromUtc;

        while (count < limit)
        {
            at = NextAfter(anchorUtc, at, zone);

            if (at > toUtc)
            {
                break;
            }

            count++;
        }

        return count;
    }

    /// <summary>The cadence in words, such as "every 2 h" or "at 07:00, 13:00 on Mon–Fri".</summary>
    public string Describe()
    {
        if (Kind == DutyCadenceKind.Every)
        {
            return Interval.TotalMinutes % 60 == 0 ? $"every {Interval.TotalHours:0} h" : $"every {Interval.TotalMinutes:0} min";
        }

        var times = string.Join(", ", Times.Select(time => time.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)));

        return Days.Count == 0 || Days.Count == 7 ? $"at {times}" : $"at {times} on {DaysText(Days)}";
    }

    private static string DaysText(IReadOnlyList<DayOfWeek> days)
    {
        DayOfWeek[] week = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];
        var chosen = week.Where(days.Contains).ToList();
        var weekdays = week[..5];

        if (chosen.Count == 5 && weekdays.All(chosen.Contains))
        {
            return "Mon–Fri";
        }

        return string.Join(", ", chosen.Select(day => day.ToString()[..3]));
    }
}
