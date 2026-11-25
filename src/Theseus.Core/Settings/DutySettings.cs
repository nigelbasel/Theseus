namespace Theseus.Core.Settings;

/// <summary>The board's limits for duties and the overnight queue, set in Options.</summary>
public sealed record DutySettings
{
    /// <summary>Runs a duty may give in one local day unless it sets its own cap.</summary>
    public int DailyRunCap { get; init; } = 24;

    /// <summary>Runs in a row that crash or are not delivered before a duty pauses itself.</summary>
    public int FailureLimit { get; init; } = 3;

    /// <summary>Whether quiet hours apply. Off by default.</summary>
    public bool QuietHoursOn { get; init; }

    /// <summary>When quiet hours begin, local time.</summary>
    public TimeOnly QuietFrom { get; init; } = new(22, 0);

    /// <summary>When quiet hours end, local time.</summary>
    public TimeOnly QuietUntil { get; init; } = new(7, 0);

    /// <summary>
    /// The projects whose overnight queue is on, by project name. The queue acts on nobody's request, so it is off unless
    /// the owner lists the project (Nigel, 2026-11-24).
    /// </summary>
    public IReadOnlyList<string> QueueProjects { get; init; } = [];

    /// <summary>How long a card must have been idle before the queue resumes it.</summary>
    public TimeSpan QueueIdleThreshold { get; init; } = TimeSpan.FromMinutes(30);

    /// <summary>How many times the queue may resume one card in a local day.</summary>
    public int QueueDailyCapPerCard { get; init; } = 6;

    /// <summary>Whether <paramref name="localTime"/> falls in quiet hours, which may run past midnight.</summary>
    public bool IsQuiet(TimeOnly localTime)
    {
        if (!QuietHoursOn || QuietFrom == QuietUntil)
        {
            return false;
        }

        return QuietFrom < QuietUntil
            ? localTime >= QuietFrom && localTime < QuietUntil
            : localTime >= QuietFrom || localTime < QuietUntil;
    }

    /// <summary>Whether the overnight queue is on for <paramref name="project"/>.</summary>
    public bool QueueIsOn(string? project) =>
        project is not null && QueueProjects.Contains(project, StringComparer.OrdinalIgnoreCase);
}
