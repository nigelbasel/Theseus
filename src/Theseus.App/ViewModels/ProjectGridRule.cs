using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// Which cards leave the board's grid (project-view AC 13): those a readable project's file names (1.1c AC 1), and a
/// verifier of one. A pinned or unreadable project names no one, so its cards stay on the grid and a broken file never
/// hides live work. Decided by looking the card up, not by a value on the card, so a card a refresh has just rebuilt,
/// which knows no project until the shell sets it, is placed rightly at once.
/// </summary>
public static class ProjectGridRule
{
    /// <summary>The rule for the board, or null when no card is in a project and every card stays in the grid (AC 22).</summary>
    /// <param name="memberOf">A card's place in the projects, or null.</param>
    /// <param name="anyMembers">Whether any card is in a project at all.</param>
    public static Func<WorkstreamCardViewModel, bool>? KeepOutOfGrid(Func<WorkstreamCardViewModel, ProjectMember?> memberOf, bool anyMembers)
    {
        ArgumentNullException.ThrowIfNull(memberOf);

        return anyMembers ? card => memberOf(card) is not null : null;
    }
}
