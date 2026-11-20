using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// Which cards leave the board's grid (project-view AC 13): those of a project that reads. A project that is
/// unreadable, pinned, or whose repository is no longer listed has no project card, so its cards stay on the grid
/// and a broken parse never hides live work.
/// </summary>
public static class ProjectGridRule
{
    /// <summary>The rule for the board, or null when no project reads and every card stays in the grid (AC 22).</summary>
    public static Func<WorkstreamCardViewModel, bool>? KeepOutOfGrid(IReadOnlyList<ProjectFileReading> readings)
    {
        ArgumentNullException.ThrowIfNull(readings);

        var names = readings.Where(reading => reading.IsReadable).Select(reading => reading.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return names.Count == 0 ? null : card => card.Workstream.Project is { } project && names.Contains(project);
    }
}
