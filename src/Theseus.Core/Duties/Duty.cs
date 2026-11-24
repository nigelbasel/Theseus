namespace Theseus.Core.Duties;

/// <summary>
/// An instruction Theseus gives one card again and again on a cadence, so periodic work needs no loop in a session's tab
/// that dies with it and that nobody else can see or stop (Nigel, 2026-11-24).
/// </summary>
/// <param name="Id">"d-" and eight hex digits.</param>
/// <param name="Card">The card's key.</param>
/// <param name="Name">Short, unique on its card.</param>
/// <param name="Instruction">What the card is told each run; may run to several lines.</param>
/// <param name="Cadence">When it is due.</param>
/// <param name="End">When it ends of itself.</param>
/// <param name="SetBy">The key of the card whose session set it, or null when the owner did.</param>
/// <param name="SetUtc">When it was set: the grid an Every cadence counts from.</param>
/// <param name="State">Where its runs stand.</param>
public sealed record Duty(
    string Id,
    string Card,
    string Name,
    string Instruction,
    DutyCadence Cadence,
    DutyEnd End,
    string? SetBy,
    DateTimeOffset SetUtc,
    DutyRunState State)
{
    /// <summary><see cref="PausedBy"/> when the owner paused it.</summary>
    public const string Owner = "owner";

    /// <summary><see cref="PausedBy"/> when Theseus paused it after failed runs.</summary>
    public const string Theseus = "theseus";

    /// <summary>Its own daily cap, or null to take the board's.</summary>
    public int? DailyRunCap { get; init; }

    /// <summary>Whether it runs through quiet hours.</summary>
    public bool ThroughQuietHours { get; init; }

    /// <summary>Who paused it: <see cref="Owner"/>, <see cref="Theseus"/> or a card's key; null when it is not paused.</summary>
    public string? PausedBy { get; init; }

    /// <summary>Why it is paused.</summary>
    public string? PauseReason { get; init; }

    /// <summary>When it ended, or null while it is live.</summary>
    public DateTimeOffset? EndedUtc { get; init; }

    /// <summary>Why it ended.</summary>
    public string? EndReason { get; init; }

    /// <summary>Whether it is paused.</summary>
    public bool IsPaused => PausedBy is not null;

    /// <summary>Whether it has ended.</summary>
    public bool IsEnded => EndedUtc is not null;

    /// <summary>Whether the owner set it, so no session may change it.</summary>
    public bool IsOwners => SetBy is null;

    /// <summary>The instruction's first line, cut to <paramref name="length"/> characters.</summary>
    public string FirstLine(int length = 80)
    {
        var line = Instruction.Split('\n')[0].Trim();

        return line.Length <= length ? line : $"{line[..(length - 1)]}…";
    }
}
