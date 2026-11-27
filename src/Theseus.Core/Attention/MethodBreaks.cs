using Theseus.Core.Persistence;

namespace Theseus.Core.Attention;

/// <summary>
/// The method breaks this build introduces, and what reports say about them (7.3). Attended time was unmeasured from
/// late October, when the owner moved into Theseus's own windows, until this build; it is labelled as such, never as 0,
/// until two clean weeks have been measured the new way (decision, 2026-11-27).
/// </summary>
public static class MethodBreaks
{
    /// <summary>The 7.3 change.</summary>
    public const string AllTimeRecorded = "7.3-all-time-recorded";

    /// <summary>What it changed, as a report shows it.</summary>
    public const string AllTimeRecordedChange =
        "machine time counts every busy session of a card and the ledger's headless runs, less lock waits; attended time "
        + "counts Theseus's own windows as well as the terminal";

    /// <summary>The Monday of ISO week 44, the first week attended time went unrecorded.</summary>
    public static readonly DateTimeOffset UnmeasuredSince = new(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);

    /// <summary>How long attended time must have been measured the new way before a report shows it without a caveat.</summary>
    public static readonly TimeSpan CleanSpell = TimeSpan.FromDays(14);

    /// <summary>The label attended time carries while it cannot be trusted.</summary>
    public const string UnmeasuredLabel = "unmeasured since late October";

    /// <summary>Records the 7.3 break the first time this build runs, and returns every break recorded.</summary>
    public static IReadOnlyList<MethodBreak> Ensure(JsonLinesFile<MethodBreak> log, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(log);

        var breaks = log.Read();

        if (breaks.Any(each => each.Id == AllTimeRecorded))
        {
            return breaks;
        }

        var added = new MethodBreak(AllTimeRecorded, now, AllTimeRecordedChange);
        log.Append(added);

        return [.. breaks, added];
    }

    /// <summary>When the 7.3 break was recorded, or null when it has not been.</summary>
    public static DateTimeOffset? AllTimeRecordedAt(IReadOnlyList<MethodBreak> breaks) =>
        breaks?.FirstOrDefault(each => each.Id == AllTimeRecorded)?.At;

    /// <summary>
    /// Whether attended time over a range must be labelled unmeasured: the range touches the weeks from late October to two
    /// weeks after the break, or there has been no break yet.
    /// </summary>
    public static bool AttendedIsUnmeasured(DateTimeOffset from, DateTimeOffset to, IReadOnlyList<MethodBreak> breaks)
    {
        var reliableFrom = AllTimeRecordedAt(breaks) is { } at ? at + CleanSpell : DateTimeOffset.MaxValue;

        return to > UnmeasuredSince && from < reliableFrom;
    }

    /// <summary>The breaks inside a range, each as a report's sentence.</summary>
    public static IReadOnlyList<string> Spanned(DateTimeOffset from, DateTimeOffset to, IReadOnlyList<MethodBreak> breaks)
    {
        ArgumentNullException.ThrowIfNull(breaks);

        return [.. breaks.Where(each => each.At > from && each.At < to).Select(each => $"Measurement changed on {each.At:yyyy-MM-dd}: {each.Change}.")];
    }
}
