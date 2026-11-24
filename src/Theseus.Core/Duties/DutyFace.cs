using System.Globalization;

namespace Theseus.Core.Duties;

/// <summary>What a card's face says about its duties: one line each, at most two, then how many more.</summary>
public static class DutyFace
{
    /// <summary>The most duty lines a card face shows.</summary>
    public const int MostLines = 2;

    /// <summary>The face's lines, such as "↻ Re-triage · every 2 h · next 23:30 · last 21:30 ✓".</summary>
    public static IReadOnlyList<string> Lines(IReadOnlyList<Duty> duties, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(duties);

        var lines = duties.Take(MostLines).Select(duty => Line(duty, zone)).ToList();

        if (duties.Count > MostLines)
        {
            lines.Add($"+ {duties.Count - MostLines} more");
        }

        return lines;
    }

    /// <summary>One duty's line.</summary>
    public static string Line(Duty duty, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(duty);

        var state = duty.State;

        if (duty.IsPaused)
        {
            return $"⏸ {duty.Name} · paused: {duty.PauseReason ?? (duty.PausedBy == Duty.Owner ? "by you" : "by a session")}";
        }

        var now = state.DeliveredUtc is not null ? "running now"
            : state.Waiting is { } waiting ? $"waiting: {waiting}"
            : $"next {Time(state.NextDueUtc, zone)}";
        var last = state.LastRunUtc is { } at ? $" · last {Time(at, zone)} {Mark(state.LastOutcome)}" : string.Empty;

        return $"↻ {duty.Name} · {duty.Cadence.Describe()} · {now}{last}".TrimEnd();
    }

    /// <summary>The face's tooltip: each duty's instruction's first line and who set it.</summary>
    public static string? ToolTip(IReadOnlyList<Duty> duties, Func<string, string> titleOf)
    {
        ArgumentNullException.ThrowIfNull(duties);
        ArgumentNullException.ThrowIfNull(titleOf);

        return duties.Count == 0
            ? null
            : string.Join(Environment.NewLine, duties.Select(duty =>
                $"{duty.Name}: {duty.FirstLine()} (set by {(duty.SetBy is { } by ? titleOf(by) : "you")}; {duty.End.Describe()})"));
    }

    private static string Mark(DutyOutcome? outcome) => outcome switch
    {
        DutyOutcome.Finished => "✓",
        DutyOutcome.Crashed => "✗ crashed",
        DutyOutcome.NotDelivered => "✗ not delivered",
        _ => string.Empty,
    };

    private static string Time(DateTimeOffset utc, TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(utc, zone);
        var today = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).Date;

        return local.Date == today
            ? local.ToString("HH:mm", CultureInfo.InvariantCulture)
            : local.ToString("ddd HH:mm", CultureInfo.InvariantCulture);
    }
}
