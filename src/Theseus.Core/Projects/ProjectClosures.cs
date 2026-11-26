using Theseus.Core.Settings;

namespace Theseus.Core.Projects;

/// <summary>
/// The closed and archived projects in the settings (archive-a-project, 2026-11-26). Only the owner closes, archives or
/// reopens a project: no tool a session can call reaches these (brief item 4).
/// </summary>
public static class ProjectClosures
{
    /// <summary>How a project was put away, or null while it is open.</summary>
    public static ProjectClosure? Of(TheseusSettings settings, string? project)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return project is null
            ? null
            : settings.ClosedProjects.FirstOrDefault(entry => string.Equals(entry.Key, project, StringComparison.OrdinalIgnoreCase)).Value;
    }

    /// <summary>Whether a project is closed or archived.</summary>
    public static bool IsClosed(TheseusSettings settings, string? project) => Of(settings, project) is not null;

    /// <summary>Puts a project away: the closure recorded, with whether its overnight queue was on, and the queue switched off.</summary>
    public static TheseusSettings Close(TheseusSettings settings, string project, ProjectClosure closure)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(closure);

        var queueOn = settings.Duties.QueueIsOn(project);

        return With(settings, project, closure with { QueueWasOn = queueOn }) with
        {
            Duties = settings.Duties with
            {
                QueueProjects = [.. settings.Duties.QueueProjects.Where(name => !string.Equals(name, project, StringComparison.OrdinalIgnoreCase))],
            },
        };
    }

    /// <summary>
    /// Opens a project again: its closure forgotten and its overnight queue back on if it was. The closure is returned so
    /// the caller resumes its duties and restores its cards; null when it was not closed.
    /// </summary>
    public static (TheseusSettings Settings, ProjectClosure? Closure) Reopen(TheseusSettings settings, string project)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(project);

        if (Of(settings, project) is not { } closure)
        {
            return (settings, null);
        }

        IReadOnlyList<string> queue = closure.QueueWasOn && !settings.Duties.QueueIsOn(project)
            ? [.. settings.Duties.QueueProjects, project]
            : settings.Duties.QueueProjects;

        return (settings with
        {
            ClosedProjects = settings.ClosedProjects
                .Where(entry => !string.Equals(entry.Key, project, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase),
            Duties = settings.Duties with { QueueProjects = queue },
        }, closure);
    }

    /// <summary>
    /// The cards of an archived project to archive now: those whose session has ended and that are not archived already.
    /// A card the owner had archived before is never recorded, so Reopen leaves it archived. Closed, none.
    /// </summary>
    public static IReadOnlyList<string> DueToArchive(ProjectClosure? closure, IReadOnlyList<ProjectCloseCard> cards)
    {
        ArgumentNullException.ThrowIfNull(cards);

        return closure is { Kind: ProjectClosureKind.Archived }
            ? [.. cards.Where(card => !card.HasSession && !card.IsArchived).Select(card => card.Key)]
            : [];
    }

    /// <summary>Records cards the archive archived, so Reopen restores them.</summary>
    public static TheseusSettings Archived(TheseusSettings settings, string project, IReadOnlyCollection<string> keys)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(keys);

        return Of(settings, project) is { } closure && keys.Count > 0
            ? With(settings, project, closure with { ArchivedCards = [.. closure.ArchivedCards.Union(keys, StringComparer.OrdinalIgnoreCase)] })
            : settings;
    }

    private static TheseusSettings With(TheseusSettings settings, string project, ProjectClosure closure)
    {
        var closed = settings.ClosedProjects
            .Where(entry => !string.Equals(entry.Key, project, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);
        closed[project] = closure;

        return settings with { ClosedProjects = closed };
    }
}
