using System.Globalization;
using Theseus.Core.Duties;

namespace Theseus.App.ViewModels;

/// <summary>
/// The duty dialog: a new duty, or changes to one, over three tabs (Duty, Limits, History). The owner's changes are his,
/// so they are saved as set by him whoever first set the duty.
/// </summary>
public sealed class DutyEditViewModel : ObservableObject
{
    private readonly DutyBook _book;
    private string? _card;
    private string _name;
    private string _instruction;
    private bool _isEvery;
    private string _every;
    private string _at;
    private string _days;
    private string _until;
    private string _cap;
    private bool _throughQuietHours;
    private string? _refusal;

    /// <summary>Creates the dialog's model.</summary>
    /// <param name="duty">The duty to change, or null for a new one.</param>
    /// <param name="card">The card it is on, or null to choose one.</param>
    /// <param name="cards">The cards a new duty may go on.</param>
    /// <param name="book">Where it is saved.</param>
    /// <param name="history">Its card's History lines for this duty, newest first.</param>
    public DutyEditViewModel(Duty? duty, string? card, IReadOnlyList<(string Key, string Title)> cards, DutyBook book,
        IReadOnlyList<string>? history = null)
    {
        _book = book;
        Duty = duty;
        _card = card ?? cards.FirstOrDefault().Key;
        Cards = [.. cards.Select(each => new KeyValuePair<string, string>(each.Key, each.Title))];
        _name = duty?.Name ?? string.Empty;
        _instruction = duty?.Instruction ?? string.Empty;
        _isEvery = duty?.Cadence.Kind != DutyCadenceKind.At;
        _every = duty is { Cadence.Kind: DutyCadenceKind.Every } ? Interval(duty.Cadence.Interval) : "1h";
        _at = duty is { Cadence.Kind: DutyCadenceKind.At }
            ? string.Join(", ", duty.Cadence.Times.Select(time => time.ToString("HH:mm", CultureInfo.InvariantCulture)))
            : "07:00";
        _days = duty is { Cadence.Kind: DutyCadenceKind.At, Cadence.Days.Count: > 0 }
            ? string.Join(", ", duty.Cadence.Days.Select(day => day.ToString()[..3]))
            : string.Empty;
        _until = duty?.End switch
        {
            { Kind: DutyEndKind.OnDate, Date: { } date } => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            { Kind: DutyEndKind.OnCriterion, Criterion: { } criterion } => $"criterion: {criterion}",
            _ => "never",
        };
        _cap = duty?.DailyRunCap?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
        _throughQuietHours = duty?.ThroughQuietHours ?? false;
        History = history ?? [];
    }

    /// <summary>The duty being changed, or null for a new one.</summary>
    public Duty? Duty { get; }

    /// <summary>The duty as saved, once OK has saved it.</summary>
    public Duty? Saved { get; private set; }

    /// <summary>The dialog's title.</summary>
    public string Title => Duty is null ? "New duty" : $"Duty: {Duty.Name}";

    /// <summary>Whether the card can be chosen: only for a new duty.</summary>
    public bool CanChooseCard => Duty is null;

    /// <summary>Whether the name can be changed: only for a new duty, since a duty is found by its name.</summary>
    public bool CanRename => Duty is null;

    /// <summary>The cards a new duty may go on.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Cards { get; }

    /// <summary>Its card's key.</summary>
    public string? Card
    {
        get => _card;
        set => Set(ref _card, value);
    }

    /// <summary>Its name.</summary>
    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    /// <summary>What its card is told each run.</summary>
    public string Instruction
    {
        get => _instruction;
        set => Set(ref _instruction, value);
    }

    /// <summary>Whether it runs every interval, rather than at set times.</summary>
    public bool IsEvery
    {
        get => _isEvery;
        set
        {
            if (Set(ref _isEvery, value))
            {
                Raise(nameof(IsAt));
            }
        }
    }

    /// <summary>Whether it runs at set times.</summary>
    public bool IsAt
    {
        get => !_isEvery;
        set => IsEvery = !value;
    }

    /// <summary>The interval, such as 20m or 2h.</summary>
    public string Every
    {
        get => _every;
        set => Set(ref _every, value);
    }

    /// <summary>The times of day, such as 07:00, 13:00.</summary>
    public string At
    {
        get => _at;
        set => Set(ref _at, value);
    }

    /// <summary>The weekdays, such as Mon-Fri; empty for every day.</summary>
    public string Days
    {
        get => _days;
        set => Set(ref _days, value);
    }

    /// <summary>When it ends: never, a date, or criterion: its text.</summary>
    public string Until
    {
        get => _until;
        set => Set(ref _until, value);
    }

    /// <summary>Its own daily cap, or empty for the board's.</summary>
    public string Cap
    {
        get => _cap;
        set => Set(ref _cap, value);
    }

    /// <summary>Whether it runs through quiet hours.</summary>
    public bool ThroughQuietHours
    {
        get => _throughQuietHours;
        set => Set(ref _throughQuietHours, value);
    }

    /// <summary>Its runs and skips, newest first.</summary>
    public IReadOnlyList<string> History { get; }

    /// <summary>Why it could not be saved, shown under the buttons; null when it was.</summary>
    public string? Refusal
    {
        get => _refusal;
        private set => Set(ref _refusal, value);
    }

    /// <summary>Saves it; false with <see cref="Refusal"/> set when it cannot be.</summary>
    public bool Save()
    {
        Refusal = Validate(out var cadence, out var end, out var cap);

        if (Refusal is not null)
        {
            return false;
        }

        var (duty, _, refusal) = _book.Set(_card!, _name, _instruction, cadence!, end!, null, cap, _throughQuietHours);
        Refusal = refusal;
        Saved = duty;

        return refusal is null;
    }

    private string? Validate(out DutyCadence? cadence, out DutyEnd? end, out int? cap)
    {
        cadence = null;
        end = null;
        cap = null;

        if (string.IsNullOrWhiteSpace(_card))
        {
            return "Choose the card it runs on.";
        }

        if (_isEvery)
        {
            if (DutyText.Interval(_every) is not { } interval)
            {
                return $"\"{_every}\" is not an interval: give one such as 20m or 2h.";
            }

            cadence = DutyCadence.Every(interval);
        }
        else
        {
            if (DutyText.Times(_at) is not { } times)
            {
                return $"\"{_at}\" is not a list of times: give them such as 07:00, 13:00.";
            }

            if (DutyText.Days(_days) is not { } days)
            {
                return $"\"{_days}\" are not weekdays: give them such as Mon-Fri, or leave it empty for every day.";
            }

            cadence = DutyCadence.At(times, days);
        }

        if (DutyText.End(_until) is not { } ends)
        {
            return $"\"{_until}\" is not an end: give never, a date yyyy-MM-dd, or criterion: its text.";
        }

        end = ends;

        if (string.IsNullOrWhiteSpace(_cap))
        {
            return null;
        }

        if (!int.TryParse(_cap, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) || number < 1)
        {
            return "The daily cap is a whole number of runs, at least 1, or empty for the board's.";
        }

        cap = number;

        return null;
    }

    private static string Interval(TimeSpan interval) =>
        interval.TotalMinutes % 60 == 0
            ? $"{interval.TotalHours.ToString("0", CultureInfo.InvariantCulture)}h"
            : $"{interval.TotalMinutes.ToString("0", CultureInfo.InvariantCulture)}m";
}
