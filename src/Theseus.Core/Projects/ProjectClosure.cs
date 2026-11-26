namespace Theseus.Core.Projects;

/// <summary>
/// A project the owner closed or archived, kept in Theseus's settings and never in project.md, which sessions write
/// (archive-a-project, 2026-11-26). It holds what the close changed, so Reopen undoes exactly that and nothing else: the
/// duties it paused, whether the overnight queue was on, and the cards it archived. Its records are never moved.
/// </summary>
/// <param name="Kind">Closed or archived.</param>
/// <param name="ClosedUtc">When the owner closed it.</param>
public sealed record ProjectClosure(ProjectClosureKind Kind, DateTimeOffset ClosedUtc)
{
    /// <summary>Whether its overnight queue was on, so Reopen switches it back on.</summary>
    public bool QueueWasOn { get; init; }

    /// <summary>The ids of the duties the close paused; Reopen resumes these and no other.</summary>
    public IReadOnlyList<string> PausedDuties { get; init; } = [];

    /// <summary>The keys of the cards the archive archived; Reopen restores these and no other.</summary>
    public IReadOnlyList<string> ArchivedCards { get; init; } = [];

    /// <summary>"Closed" or "Archived", as its card says it.</summary>
    public string Word => Kind == ProjectClosureKind.Archived ? "Archived" : "Closed";
}
