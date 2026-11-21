namespace Theseus.App.ViewModels;

/// <summary>
/// The question asked before one card of a running project is stopped or started by hand (1.1e AC 10; Nigel, 2026-11-21:
/// "This card is part of &lt;project&gt;, which decides what runs. Stop it anyway?"). A running project is one with a
/// session open and no run state; cards of a stopped project, and ad hoc cards, are not asked about.
/// </summary>
public static class ProjectCardGuard
{
    /// <summary>The question for these cards, or null when none of them is in a running project.</summary>
    /// <param name="cards">The cards about to be stopped or started.</param>
    /// <param name="projects">The project cards on the board.</param>
    /// <param name="verb">"Stop" or "Start".</param>
    public static string? Question(IEnumerable<WorkstreamCardViewModel> cards, IReadOnlyList<ProjectCardViewModel> projects, string verb)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(projects);

        var running = projects.Where(project => project is { IsLive: true, IsStopped: false }).ToList();
        var caught = cards
            .Select(card => (Card: card, Project: running.FirstOrDefault(project =>
                project.MemberKeys.Contains(card.Key, StringComparer.OrdinalIgnoreCase))))
            .Where(each => each.Project is not null)
            .ToList();

        return caught.Count switch
        {
            0 => null,
            1 => $"{caught[0].Card.Title} is part of {caught[0].Project!.Title}, which decides what runs. {verb} it anyway?",
            _ => $"{string.Join(", ", caught.Select(each => each.Card.Title))} are part of "
                 + $"{string.Join(", ", caught.Select(each => each.Project!.Title).Distinct())}, which decide what runs. {verb} them anyway?",
        };
    }
}
