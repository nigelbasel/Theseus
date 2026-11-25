namespace Theseus.Core.Duties;

/// <summary>What <c>duties.json</c> holds: every duty with its run state, and the overnight queue's memory.</summary>
/// <param name="Duties">Every duty, ended ones kept for <see cref="DutyBook.EndedKeptFor"/>.</param>
/// <param name="Queue">The queue's record of each card it has resumed.</param>
public sealed record DutyFile(IReadOnlyList<Duty> Duties, IReadOnlyList<QueueRecord> Queue)
{
    /// <summary>A file with nothing in it.</summary>
    public static readonly DutyFile Empty = new([], []);
}
