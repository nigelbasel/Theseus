using System.Globalization;

namespace Theseus.Core.Attention;

/// <summary>
/// The UTC offset stamped on a line, written as <c>+02:00</c> (7.3). Days follow it, so a past day's totals never move
/// when the owner travels between SAST and the UK (decision 17, 2026-11-27).
/// </summary>
public static class AttentionOffset
{
    /// <summary>The offset in force at a moment in a zone.</summary>
    public static string Of(DateTimeOffset at, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return Format(zone.GetUtcOffset(at.UtcDateTime));
    }

    /// <summary>Writes an offset as <c>+hh:mm</c> or <c>-hh:mm</c>.</summary>
    public static string Format(TimeSpan offset) =>
        (offset < TimeSpan.Zero ? "-" : "+") + offset.Duration().ToString(@"hh\:mm", CultureInfo.InvariantCulture);

    /// <summary>Reads an offset, or null when there is none or it does not read.</summary>
    public static TimeSpan? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2 || text[0] is not ('+' or '-'))
        {
            return null;
        }

        if (!TimeSpan.TryParseExact(text[1..], @"hh\:mm", CultureInfo.InvariantCulture, out var value))
        {
            return null;
        }

        return text[0] == '-' ? -value : value;
    }

    /// <summary>
    /// The day an interval falls on: by its own stamped offset when it has one, else by <paramref name="fallback"/>, the
    /// zone a line from before 7.3 is read in.
    /// </summary>
    public static DateOnly DayOf(AttentionInterval interval, TimeZoneInfo fallback)
    {
        ArgumentNullException.ThrowIfNull(interval);
        ArgumentNullException.ThrowIfNull(fallback);

        var offset = Parse(interval.Offset) ?? fallback.GetUtcOffset(interval.Start.UtcDateTime);

        return DateOnly.FromDateTime(interval.Start.ToOffset(offset).DateTime);
    }
}
