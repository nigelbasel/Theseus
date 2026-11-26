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

    /// <summary>Whether a project is stopped: Stop the project has run and Start the project has not since.</summary>
    public static bool IsStopped(TheseusSettings settings, string? project) => Of(settings, project) is { IsResumed: false };

    /// <summary>Records a stop, with whether its overnight queue was on, and switches the queue off.</summary>
    public static TheseusSettings Stop(TheseusSettings settings, string project, ProjectStopping stopping)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(stopping);

        var found = Of(settings, project);
        var earlier = found is { IsResumed: true } ? new ProjectStopping(found.StoppedUtc) { PendingLockCards = found.PendingLockCards } : found;
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

        if (Of(settings, project) is not { IsResumed: false } stopping)
        {
            return (settings, null);
        }

        IReadOnlyList<string> queue = stopping.QueueWasOn && !settings.Duties.QueueIsOn(project)
            ? [.. settings.Duties.QueueProjects, project]
            : settings.Duties.QueueProjects;

        var stopped = Without(settings, project);

        if (stopping.PendingLockCards.Count > 0)
        {
            stopped[project] = new ProjectStopping(stopping.StoppedUtc) { IsResumed = true, PendingLockCards = stopping.PendingLockCards };
        }

        return (settings with
        {
            StoppedProjects = stopped,
            Duties = settings.Duties with { QueueProjects = queue },
        }, stopping);
    }

    /// <summary>Takes cards whose locks were released off a stop's pending list.</summary>
    public static TheseusSettings Released(TheseusSettings settings, string project, IReadOnlyCollection<string> keys)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(keys);

        if (Of(settings, project) is not { } stopping || keys.Count == 0)
        {
            return settings;
        }

        var left = stopping with
        {
            PendingLockCards = [.. stopping.PendingLockCards.Where(key => !keys.Contains(key, StringComparer.OrdinalIgnoreCase))],
        };

        return left is { IsResumed: true, PendingLockCards.Count: 0 }
            ? settings with { StoppedProjects = Without(settings, project) }
            : With(settings, project, left);
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
