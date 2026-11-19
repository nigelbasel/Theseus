using System.Globalization;

namespace Theseus.Core.Resources;

/// <summary>What the status bar and the log say about cores held back (parked-cores spec AC 6 to 8).</summary>
public static class CoreClampText
{
    /// <summary>The status bar's text, for example "Cores held back: 16 of 20 parked under load".</summary>
    public static string Status(CoreSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        return $"Cores held back: {sample.Parked} of {sample.Total} parked under load";
    }

    /// <summary>What hovering it says: what it means, what to check, and the figures behind it.</summary>
    public static string Tooltip(CoreSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        var figures = $"{sample.Parked} of {sample.Total} parked; the rest {Whole(sample.RunningBusyPercent)}% busy"
                      + (sample.ClockPercent is { } clock ? $"; clock {Whole(clock)}% of base" : string.Empty) + ".";

        return string.Join("\n",
            "The machine is keeping most of its cores parked while the rest are saturated, so work waits.",
            "Usually heat: blocked vents, dusty fans, or a laptop on a soft surface.",
            "Sometimes the power plan's minimum cores setting (core parking).",
            figures);
    }

    /// <summary>The log line when the warning starts.</summary>
    public static string Started(CoreSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);

        return $"Cores held back: {Figures(sample)}";
    }

    /// <summary>The log line when it stops, with the sample that ended it, or null if that could not be read.</summary>
    public static string Stopped(CoreSample? sample) => sample is null
        ? "Cores no longer held back: the counters could not be read"
        : $"Cores no longer held back: {Figures(sample)}";

    private static string Figures(CoreSample sample) =>
        $"{sample.Parked} of {sample.Total} parked, the rest {Whole(sample.RunningBusyPercent)}% busy"
        + (sample.ClockPercent is { } clock ? $", clock {Whole(clock)}% of base" : string.Empty);

    private static string Whole(double value) => Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
}
