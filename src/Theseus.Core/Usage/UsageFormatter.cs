using System.Globalization;

namespace Theseus.Core.Usage;

/// <summary>Renders a usage window as the one line a status bar has room for.</summary>
public static class UsageFormatter
{
    /// <summary>A short label for a window, for example <c>5h</c> or the model it is scoped to.</summary>
    public static string Label(UsageLimit limit)
    {
        ArgumentNullException.ThrowIfNull(limit);

        if (!string.IsNullOrWhiteSpace(limit.ScopeLabel))
        {
            return limit.ScopeLabel;
        }

        return limit.Kind switch
        {
            "session" => "5h",
            "weekly_all" => "7d",
            "weekly_scoped" => "7d scoped",
            _ => limit.Kind.Replace('_', ' '),
        };
    }

    /// <summary>The window's consumption, rounded the way a percentage should be read.</summary>
    public static string Percent(UsageLimit limit)
    {
        ArgumentNullException.ThrowIfNull(limit);

        return $"{Math.Round(limit.Percent, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture)}%";
    }

    /// <summary>
    /// When the window rolls over, as time remaining rather than a clock time while it is close,
    /// because "resets in 40m" is the thing being decided on.
    /// </summary>
    public static string Reset(UsageLimit limit, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(limit);

        if (limit.ResetsAtUtc is not { } resets)
        {
            return string.Empty;
        }

        var remaining = resets - now;

        if (remaining <= TimeSpan.Zero)
        {
            return "resetting";
        }

        if (remaining < TimeSpan.FromHours(1))
        {
            return $"resets in {(int)remaining.TotalMinutes}m";
        }

        if (remaining < TimeSpan.FromHours(24))
        {
            var hours = (int)remaining.TotalHours;
            var minutes = remaining.Minutes;

            return minutes == 0 ? $"resets in {hours}h" : $"resets in {hours}h {minutes}m";
        }

        return $"resets {resets.ToLocalTime().ToString("ddd HH:mm", CultureInfo.CurrentCulture)}";
    }

    /// <summary>The whole line for one window.</summary>
    public static string Describe(UsageLimit limit, DateTimeOffset now)
    {
        var reset = Reset(limit, now);

        return reset.Length == 0
            ? $"{Label(limit)} {Percent(limit)}"
            : $"{Label(limit)} {Percent(limit)} · {reset}";
    }
}
