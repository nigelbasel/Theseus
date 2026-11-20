using Theseus.Core.Domain;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// The Project menu on a card and on the menu bar (project-view AC 4): each readable project, offering the slugs its
/// file lists and No slug yet, then Not in a project. Empty while no project is readable, so a board with no projects
/// shows no new item (AC 22).
/// </summary>
public static class ProjectMenu
{
    /// <summary>What a card that is in a project and has no slug is offered to keep it so.</summary>
    public const string NoSlugYet = "No slug yet";

    /// <summary>The item that takes a card out of every project.</summary>
    public const string NotInAProject = "Not in a project";

    /// <summary>Builds the menu.</summary>
    /// <param name="readings">Every project as last read.</param>
    /// <param name="card">The card whose membership is ticked, or null on the menu bar, where nothing is.</param>
    /// <param name="choose">Applies a choice: a project and slug, a project with no slug, or two nulls for none.</param>
    public static IReadOnlyList<ProjectChoice> For(IReadOnlyList<ProjectFileReading> readings, Workstream? card,
        Action<string?, string?> choose)
    {
        ArgumentNullException.ThrowIfNull(readings);
        ArgumentNullException.ThrowIfNull(choose);

        if (!readings.Any(reading => reading.IsReadable))
        {
            return [];
        }

        var items = readings.Select(reading => reading.File is { } file
            ? Project(file, card, choose)
            : new ProjectChoice(Escape(reading.Name), false, null, [], reading.Describe, IsEnabled: false)).ToList();

        items.Add(new ProjectChoice(NotInAProject, card is not null && card.Project is null,
            new RelayCommand(_ => choose(null, null)), [], "Ad hoc work: on the board as today, in no project"));

        return items;
    }

    private static ProjectChoice Project(ProjectFile file, Workstream? card, Action<string?, string?> choose)
    {
        var here = card is not null && string.Equals(card.Project, file.Name, StringComparison.OrdinalIgnoreCase);
        var slugs = new List<ProjectChoice>
        {
            new(NoSlugYet, here && string.IsNullOrWhiteSpace(card!.Slug), new RelayCommand(_ => choose(file.Name, null)), [],
                $"In {file.Title}, with no slug yet"),
        };

        foreach (var objective in file.Objectives)
        {
            slugs.AddRange(objective.Workstreams.Select(row => new ProjectChoice(
                Escape(row.Slug),
                here && string.Equals(card!.Slug, row.Slug, StringComparison.OrdinalIgnoreCase),
                new RelayCommand(_ => choose(file.Name, row.Slug)),
                [],
                $"{row.Workstream}, objective {objective.Number}: {objective.Title}")));
        }

        return new ProjectChoice(Escape(file.Name), here, null, slugs, file.Title);
    }

    private static string Escape(string text) => text.Replace("_", "__", StringComparison.Ordinal);
}
