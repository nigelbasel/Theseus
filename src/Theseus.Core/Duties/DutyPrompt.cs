using System.Globalization;

namespace Theseus.Core.Duties;

/// <summary>
/// What a card is told when a duty runs: which duty and which run, the instruction, and what Theseus recorded for the card
/// since the last run. It claims nothing Theseus does not record, such as changes in a tracker or a repo.
/// </summary>
public static class DutyPrompt
{
    /// <summary>Said when Theseus recorded nothing for the card since the last run.</summary>
    public const string NothingRecorded = "nothing recorded";

    /// <summary>The closing line, so a run with nothing to do costs one line.</summary>
    public const string NothingNewLine = "If nothing has changed for you, say \"nothing new\" in one line and end your turn.";

    /// <summary>The text for the duty's next run.</summary>
    /// <param name="changes">The card's History entries since the last run, oldest first.</param>
    /// <param name="missed">Due times folded into this run.</param>
    /// <param name="zone">The local time zone, for the last run's time.</param>
    public static string For(Duty duty, IReadOnlyList<string> changes, int missed, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(duty);
        ArgumentNullException.ThrowIfNull(changes);

        var state = duty.State;
        var last = state.LastRunUtc is { } at
            ? $"last run {TimeZoneInfo.ConvertTime(at, zone).ToString("HH:mm", CultureInfo.InvariantCulture)}, {Word(state.LastOutcome)}"
            : "first run";
        var catchingUp = missed > 0 ? $"; catching up: {missed} run{(missed == 1 ? string.Empty : "s")} missed" : string.Empty;
        var header = $"Duty \"{duty.Name}\" ({duty.Cadence.Describe()}; run {state.RunCount + 1}; {last}{catchingUp}).";
        var since = changes.Count == 0 ? NothingRecorded : string.Join(Environment.NewLine, changes.Select(each => $"- {each}"));
        var lead = changes.Count == 0 ? "Since the last run, as far as Theseus knows: " : $"Since the last run, as far as Theseus knows:{Environment.NewLine}";

        return string.Join(Environment.NewLine + Environment.NewLine,
            $"{header} {duty.Instruction}",
            $"{lead}{since}.{Environment.NewLine}{NothingNewLine}");
    }

    /// <summary>An outcome in the words the card face uses.</summary>
    public static string Word(DutyOutcome? outcome) => outcome switch
    {
        DutyOutcome.Finished => "finished",
        DutyOutcome.Crashed => "crashed",
        DutyOutcome.NotDelivered => "not delivered",
        _ => "still going",
    };
}
