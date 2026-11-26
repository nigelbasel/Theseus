using Theseus.Core.Settings;

namespace Theseus.Core.Projects;

/// <summary>
/// The projects stopped with Stop the project, in the settings (project-view-as-tabs, Nigel 2026-11-26). Only the owner
/// stops a project this way; Start the project resumes it.
/// </summary>
public static class ProjectStops
{
    /// <summary>How a project was stopped, or null when it was not.</summary>
    public static ProjectStopping? Of(TheseusSettings settings, string? project)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return project is null
            ? null
            : settings.StoppedProjects.FirstOrDefault(entry => string.Equals(entry.Key, project, StringComparison.OrdinalIgnoreCase)).Value;
    }

    /// <summary>Records a stop, with whether its overnight queue was on, and switches the queue off.</summary>
    public static TheseusSettings Stop(TheseusSettings settings, string project, ProjectStopping stopping)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(stopping);

        var earlier = Of(settings, project);
        var queueOn = settings.Duties.QueueIsOn(project) || earlier?.QueueWasOn == true;
        var record = stopping with
        {
            QueueWasOn = queueOn,
            PausedDuties = [.. (earlier?.PausedDuties ?? []).Union(stopping.PausedDuties, StringComparer.OrdinalIgnoreCase)],
            PendingLockCards = [.. (earlier?.PendingLockCards ?? []).Union(stopping.PendingLockCards, StringComparer.OrdinalIgnoreCase)],
        };

        return With(settings, project, record) with
        {
            Duties = settings.Duties with
            {
                QueueProjects = [.. settings.Duties.QueueProjects.Where(name => !string.Equals(name, project, StringComparison.OrdinalIgnoreCase))],
            },
        };
    }

    /// <summary>
    /// Start the project: the stop forgotten and the overnight queue back on if it was. The stop is returned so the caller
    /// resumes the duties it paused; null, and the settings unchanged, when it was not stopped.
    /// </summary>
    public static (TheseusSettings Settings, ProjectStopping? Stopping) Resume(TheseusSettings settings, string project)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(project);

        if (Of(settings, project) is not { } stopping)
        {
            return (settings, null);
        }

        IReadOnlyList<string> queue = stopping.QueueWasOn && !settings.Duties.QueueIsOn(project)
            ? [.. settings.Duties.QueueProjects, project]
            : settings.Duties.QueueProjects;

        return (settings with
        {
            StoppedProjects = Without(settings, project),
            Duties = settings.Duties with { QueueProjects = queue },
        }, stopping);
    }

    /// <summary>Takes cards whose locks were released off a stop's pending list.</summary>
    public static TheseusSettings Released(TheseusSettings settings, string project, IReadOnlyCollection<string> keys)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(keys);

        return Of(settings, project) is { } stopping && keys.Count > 0
            ? With(settings, project, stopping with
            {
                PendingLockCards = [.. stopping.PendingLockCards.Where(key => !keys.Contains(key, StringComparer.OrdinalIgnoreCase))],
            })
            : settings;
    }

    private static Dictionary<string, ProjectStopping> Without(TheseusSettings settings, string project) =>
        settings.StoppedProjects
            .Where(entry => !string.Equals(entry.Key, project, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.OrdinalIgnoreCase);

    private static TheseusSettings With(TheseusSettings settings, string project, ProjectStopping stopping)
    {
        var stopped = Without(settings, project);
        stopped[project] = stopping;

        return settings with { StoppedProjects = stopped };
    }
}
