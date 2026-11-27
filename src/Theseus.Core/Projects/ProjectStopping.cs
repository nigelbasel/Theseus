namespace Theseus.Core.Projects;

/// <summary>
/// A project the owner stopped with Stop the project (project-view-as-tabs AC 10, 11), kept in Theseus's settings: what the
/// stop changed, so Start undoes exactly that, and the cards whose locks are still to be released once their sessions
/// end, so a restart of Theseus does not lose them. The project stays open.
/// </summary>
/// <param name="StoppedUtc">When the owner stopped it.</param>
public sealed record ProjectStopping(DateTimeOffset StoppedUtc)
{
    /// <summary>Whether its overnight queue was on, so Start switches it back on.</summary>
    public bool QueueWasOn { get; init; }

    /// <summary>The ids of the duties the stop paused; Start resumes these and no other.</summary>
    public IReadOnlyList<string> PausedDuties { get; init; } = [];

    /// <summary>
    /// Whether Start the project has resumed it while some of its cards' locks were still to release: it is not stopped, and
    /// only the pending releases are kept, until the last is done.
    /// </summary>
    public bool IsResumed { get; init; }

    /// <summary>The keys of the cards whose holds and queue places are released once their sessions have ended.</summary>
    public IReadOnlyList<string> PendingLockCards { get; init; } = [];
}
