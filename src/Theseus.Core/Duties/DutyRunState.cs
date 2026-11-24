namespace Theseus.Core.Duties;

/// <summary>Where a duty's runs stand. Kept with the duty, so it survives a restart.</summary>
/// <param name="NextDueUtc">The due time it waits for. Once passed, the duty is due until it runs or is skipped.</param>
public sealed record DutyRunState(DateTimeOffset NextDueUtc)
{
    /// <summary>When the last run was given to the card.</summary>
    public DateTimeOffset? LastRunUtc { get; init; }

    /// <summary>How the last finished, crashed or undelivered run went, or null before the first.</summary>
    public DutyOutcome? LastOutcome { get; init; }

    /// <summary>Why, for a crash or a run not delivered.</summary>
    public string? LastDetail { get; init; }

    /// <summary>How many runs it has given, all told.</summary>
    public int RunCount { get; init; }

    /// <summary>The local day <see cref="RunsThatDay"/> counts for.</summary>
    public DateOnly? CountedDay { get; init; }

    /// <summary>Runs given on <see cref="CountedDay"/>, for the daily cap.</summary>
    public int RunsThatDay { get; init; }

    /// <summary>Runs in a row that crashed or were not delivered.</summary>
    public int FailuresInRow { get; init; }

    /// <summary>When the run in flight was given, or null when none is.</summary>
    public DateTimeOffset? DeliveredUtc { get; init; }

    /// <summary>Whether the card has been seen working since the run in flight was given.</summary>
    public bool SeenWorking { get; init; }

    /// <summary>Why a due run is waiting, or null when it is not.</summary>
    public string? Waiting { get; init; }

    /// <summary>When a run was last skipped.</summary>
    public DateTimeOffset? LastSkipUtc { get; init; }

    /// <summary>Why it was skipped.</summary>
    public string? LastSkipReason { get; init; }

    /// <summary>Whether the owner asked for a run now: it runs at once, whatever the cadence, and does not move it.</summary>
    public bool RunNowRequested { get; init; }

    /// <summary>Runs given today, by the local day of <paramref name="today"/>.</summary>
    public int RunsOn(DateOnly today) => CountedDay == today ? RunsThatDay : 0;
}
