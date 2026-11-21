using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// What Add to project… asks and writes (1.1c AC 7; 1.1e AC 6), apart from the shell so its wiring can be tested: the
/// readable projects offered, the project asked from chosen first, and the row the choice makes.
/// </summary>
public static class AddToProjectFlow
{
    /// <summary>What the status bar says when no project reads.</summary>
    public const string NoProjectReads = "No project reads yet: start one with New project…, or set the projects folder in Tools > Options.";

    /// <summary>A row's branch: the project's primary branch's prefix and the records folder's name.</summary>
    public static string BranchFor(ProjectFile file, string name)
    {
        ArgumentNullException.ThrowIfNull(file);

        return file.PrimaryBranch.LastIndexOf('/') is var slash and > 0 ? $"{file.PrimaryBranch[..slash]}/{name}" : name;
    }

    /// <summary>
    /// Asks which project and objective a card joins, with <paramref name="chosen"/> chosen first, and returns the row to
    /// write, or null when nothing is to be written; <paramref name="notice"/> says why when that is worth saying.
    /// </summary>
    /// <param name="readings">The projects as last read.</param>
    /// <param name="key">The card's key.</param>
    /// <param name="title">Its name on screen.</param>
    /// <param name="repos">Its Repos cell.</param>
    /// <param name="chosen">The project it was asked from, or null from the board.</param>
    /// <param name="ask">The window: the options, the name to start with and the project chosen.</param>
    /// <param name="notice">Why nothing is asked, or null.</param>
    public static ProjectRowAddition? Ask(IReadOnlyList<ProjectFileReading> readings, string key, string title, string repos,
        string? chosen, Func<IReadOnlyList<ProjectOption>, string, string?, AddToProjectChoice?>? ask, out string? notice)
    {
        ArgumentNullException.ThrowIfNull(readings);

        notice = null;
        var readable = readings.Where(reading => reading.IsReadable).ToList();
        var options = readable
            .Select(reading => new ProjectOption(reading.Name, $"{reading.File!.Title} ({reading.Name})",
                [.. reading.File.Objectives.Select(objective => (objective.Number, objective.Title))]))
            .ToList();

        if (options.Count == 0)
        {
            notice = NoProjectReads;

            return null;
        }

        var name = ProjectMembership.IsSlug(key) ? key : string.Empty;

        if (ask?.Invoke(options, name, chosen) is not { } choice
            || readable.FirstOrDefault(reading => string.Equals(reading.Name, choice.Project, StringComparison.OrdinalIgnoreCase))?.File is not { } file)
        {
            return null;
        }

        return new ProjectRowAddition(choice.Project, choice.Objective,
            new ProjectWorkstreamRow(key, choice.Slug, BranchFor(file, choice.Slug), repos, 0),
            $"{title} is in {choice.Project} as {choice.Slug}, under objective {choice.Objective}.");
    }
}
