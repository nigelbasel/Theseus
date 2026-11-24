using System.Globalization;
using Theseus.Core.Duties;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// Options › Duties: the board's daily cap, the failures a duty takes before it pauses itself, quiet hours, and the
/// overnight queue's idle threshold and cap. Each is a line with its value and a Change…; each change is saved at once.
/// </summary>
public sealed class DutyOptionsViewModel : ObservableObject
{
    private readonly ISettingsStore _store;

    /// <summary>Creates the page over the settings store.</summary>
    public DutyOptionsViewModel(ISettingsStore store) => _store = store;

    private DutySettings Current => _store.Read().Duties;

    /// <summary>The daily cap as Options shows it.</summary>
    public string CapValue => $"{Current.DailyRunCap} runs a day for each duty";

    /// <summary>The failure limit as Options shows it.</summary>
    public string FailureValue => $"After {Current.FailureLimit} failed runs in a row";

    /// <summary>Quiet hours as Options shows them.</summary>
    public string QuietValue => Current.QuietHoursOn
        ? $"{Time(Current.QuietFrom)} to {Time(Current.QuietUntil)}"
        : "Off";

    /// <summary>The queue's threshold and cap as Options shows them.</summary>
    public string QueueValue =>
        $"After {Current.QueueIdleThreshold.TotalMinutes:0} minutes idle; at most {Current.QueueDailyCapPerCard} times a card a day";

    /// <summary>The projects whose queue is on, as Options shows them.</summary>
    public string QueueProjectsValue => Current.QueueProjects.Count == 0 ? "None" : string.Join(", ", Current.QueueProjects);

    /// <summary>The cap's text for its Change… box.</summary>
    public string CapText => Current.DailyRunCap.ToString(CultureInfo.InvariantCulture);

    /// <summary>The failure limit's text for its Change… box.</summary>
    public string FailureText => Current.FailureLimit.ToString(CultureInfo.InvariantCulture);

    /// <summary>Quiet hours' text for its Change… box: "off", or "22:00-07:00".</summary>
    public string QuietText => Current.QuietHoursOn ? $"{Time(Current.QuietFrom)}-{Time(Current.QuietUntil)}" : "off";

    /// <summary>The queue's threshold and cap for its Change… box: "30m, 6".</summary>
    public string QueueText => $"{Current.QueueIdleThreshold.TotalMinutes:0}m, {Current.QueueDailyCapPerCard}";

    /// <summary>Sets the daily cap; why not, or null once saved.</summary>
    public string? SetCap(string text) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cap) && cap >= 1
            ? Save(duties => duties with { DailyRunCap = cap })
            : "Give a whole number of runs, at least 1.";

    /// <summary>Sets the failure limit; why not, or null once saved.</summary>
    public string? SetFailures(string text) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit) && limit >= 1
            ? Save(duties => duties with { FailureLimit = limit })
            : "Give a whole number of runs, at least 1.";

    /// <summary>Sets quiet hours from "off" or "22:00-07:00"; why not, or null once saved.</summary>
    public string? SetQuiet(string text)
    {
        var trimmed = text.Trim();

        if (trimmed.Equals("off", StringComparison.OrdinalIgnoreCase) || trimmed.Length == 0)
        {
            return Save(duties => duties with { QuietHoursOn = false });
        }

        var parts = trimmed.Split(['-', '–'], 2);

        return parts.Length == 2 && DutyText.Times(parts[0]) is [var from] && DutyText.Times(parts[1]) is [var until] && from != until
            ? Save(duties => duties with { QuietHoursOn = true, QuietFrom = from, QuietUntil = until })
            : "Give off, or a start and an end such as 22:00-07:00.";
    }

    /// <summary>Sets the queue's threshold and cap from "30m, 6"; why not, or null once saved.</summary>
    public string? SetQueue(string text)
    {
        var parts = text.Split(',', 2);

        return parts.Length == 2 && DutyText.Interval(parts[0]) is { } idle && idle >= TimeSpan.FromMinutes(5)
               && int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cap) && cap >= 1
            ? Save(duties => duties with { QueueIdleThreshold = idle, QueueDailyCapPerCard = cap })
            : "Give the idle time, at least 5 minutes, then the most times a day, such as 30m, 6.";
    }

    private string? Save(Func<DutySettings, DutySettings> change)
    {
        var settings = _store.Read();
        _store.Write(settings with { Duties = change(settings.Duties) });

        foreach (var name in new[] { nameof(CapValue), nameof(FailureValue), nameof(QuietValue), nameof(QueueValue), nameof(QueueProjectsValue),
                     nameof(CapText), nameof(FailureText), nameof(QuietText), nameof(QueueText) })
        {
            Raise(name);
        }

        return null;
    }

    private static string Time(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
