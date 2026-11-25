namespace Theseus.Core.Duties;

/// <summary>The overnight queue's memory of one card: when it last resumed it, at what status, and how often today.</summary>
/// <param name="Card">The card's key.</param>
/// <param name="LastResumedUtc">When the queue last resumed it.</param>
/// <param name="StatusAtResume">Its piece's status line then. The queue does not resume it again until this changes.</param>
/// <param name="CountedDay">The local day <paramref name="ResumesThatDay"/> counts for.</param>
/// <param name="ResumesThatDay">Resumes on that day, for the per-card cap.</param>
public sealed record QueueRecord(string Card, DateTimeOffset LastResumedUtc, string StatusAtResume, DateOnly CountedDay, int ResumesThatDay)
{
    /// <summary>Resumes given on <paramref name="today"/>.</summary>
    public int ResumesOn(DateOnly today) => CountedDay == today ? ResumesThatDay : 0;
}
