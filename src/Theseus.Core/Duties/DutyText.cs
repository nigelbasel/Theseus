using System.Globalization;
using System.Text.RegularExpressions;

namespace Theseus.Core.Duties;

/// <summary>Reads a cadence and an end from the words a session or the owner types, such as "20m", "07:00, 13:00" or "Mon-Fri".</summary>
public static partial class DutyText
{
    private static readonly DayOfWeek[] Week =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday];

    /// <summary>An interval such as "20m", "20 min", "2h", "1.5 hours" or "90"; minutes when no unit is given.</summary>
    public static TimeSpan? Interval(string? text)
    {
        var match = IntervalPattern().Match(text?.Trim() ?? string.Empty);

        if (!match.Success || !double.TryParse(match.Groups["n"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        return match.Groups["unit"].Value.StartsWith("h", StringComparison.OrdinalIgnoreCase) ? TimeSpan.FromHours(amount) : TimeSpan.FromMinutes(amount);
    }

    /// <summary>Times of day such as "07:00, 13:30"; null when any one cannot be read.</summary>
    public static IReadOnlyList<TimeOnly>? Times(string? text)
    {
        var parts = (text ?? string.Empty).Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries);
        var times = new List<TimeOnly>();

        foreach (var part in parts)
        {
            if (!TimeOnly.TryParseExact(part, ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time))
            {
                return null;
            }

            times.Add(time);
        }

        return times.Count == 0 ? null : times;
    }

    /// <summary>Weekdays such as "Mon-Fri", "Mon, Wed, Fri" or "every day"; empty for every day; null when they cannot be read.</summary>
    public static IReadOnlyList<DayOfWeek>? Days(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0 || trimmed.Equals("every day", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("daily", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var days = new List<DayOfWeek>();

        foreach (var part in trimmed.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            var range = part.Split(['-', '–'], 2);

            if (Day(range[0]) is not { } first)
            {
                return null;
            }

            if (range.Length == 1)
            {
                days.Add(first);
                continue;
            }

            if (Day(range[1]) is not { } last)
            {
                return null;
            }

            var from = Array.IndexOf(Week, first);
            var to = Array.IndexOf(Week, last);

            for (var index = from; ; index = (index + 1) % 7)
            {
                days.Add(Week[index]);

                if (index == to)
                {
                    break;
                }
            }
        }

        return [.. days.Distinct()];
    }

    /// <summary>
    /// An end such as "never", "2026-12-01" or "criterion: the duty runs across a restart"; null when it cannot be read.
    /// </summary>
    public static DutyEnd? End(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;

        if (trimmed.Length == 0 || trimmed.Equals("never", StringComparison.OrdinalIgnoreCase))
        {
            return DutyEnd.Never;
        }

        if (DateOnly.TryParseExact(trimmed, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return new DutyEnd(DutyEndKind.OnDate, date);
        }

        const string prefix = "criterion:";

        return trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && trimmed[prefix.Length..].Trim() is { Length: > 0 } criterion
            ? new DutyEnd(DutyEndKind.OnCriterion, Criterion: criterion)
            : null;
    }

    private static DayOfWeek? Day(string text) =>
        Week.Cast<DayOfWeek?>().FirstOrDefault(day => text.Length >= 2 && day!.Value.ToString().StartsWith(text, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"^(?<n>\d+(\.\d+)?)\s*(?<unit>m|min|mins|minute|minutes|h|hr|hrs|hour|hours)?$", RegexOptions.IgnoreCase)]
    private static partial Regex IntervalPattern();
}
