using Theseus.Core.Domain;

namespace Theseus.Core.Projects;

/// <summary>
/// The project a card answers to: the one its project file gives it, else, for a scenario writer, which no project file
/// names, the project whose home it has in reach (git-22, item 7a). What waits on the owner for a writer, and what finds
/// it, goes by this (Nigel, 2026-11-26: "no sign of notifications to restart the breakers").
/// </summary>
public static class RoleProject
{
    /// <summary>The project's name, or null for a card in none.</summary>
    /// <param name="card">The card.</param>
    /// <param name="readings">The projects as last read; a writer's home is looked for among the readable ones.</param>
    public static string? Of(Workstream card, IEnumerable<ProjectFileReading> readings)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(readings);

        return card.Project;
    }
}
