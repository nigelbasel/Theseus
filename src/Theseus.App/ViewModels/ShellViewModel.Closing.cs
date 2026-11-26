using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// Close, Archive and Reopen of a project, on its board card's menu, its tab's menu and the project view's More menu
/// (archive-a-project, Nigel 2026-11-26: "there is no way to archive or close a project"). The owner's alone: no session
/// tool reaches them, and a session starts none of a closed project's cards.
/// </summary>
public sealed partial class ShellViewModel
{
    /// <summary>
    /// Asks once before a project is closed or archived: the caption, the question and the action button; true to go on.
    /// Unset, nothing is closed.
    /// </summary>
    public Func<string, string, string, bool>? ConfirmProjectClose { get; set; }

    private ProjectCloser Closer => new(_settings, Board.Card, project => Runner.Stop(project), Duties,
        card => Board.SetArchived(card, true), keys => Board.Restore(keys),
        (caption, question, action) => ConfirmProjectClose?.Invoke(caption, question, action) == true,
        text => Notice = text, () => DateTimeOffset.UtcNow)
    {
        Changed = Refresh,
        OthersOf = name => [.. Board.Cards.Where(card => string.Equals(card.Workstream.Project, name, StringComparison.OrdinalIgnoreCase))
            .Select(card => card.Key)],
        StopCards = cards => BeginStop(StepsFor(cards)),
    };

    private bool IsProjectClosed(string project) => ProjectClosures.IsClosed(_settings.Read(), project);

    /// <summary>Close or Archive of a project by name, from wherever it was asked; the view goes back to the board if it was open.</summary>
    private void PutProjectAway(string name, ProjectClosureKind kind)
    {
        if (Board.ProjectCards.FirstOrDefault(card => string.Equals(card.Name, name, StringComparison.OrdinalIgnoreCase)
                                                      && card is { IsOpenProject: true }) is not { } project)
        {
            return;
        }

        var wasOpen = string.Equals(_openProjectName, name, StringComparison.OrdinalIgnoreCase);

        if (Closer.Close(project, kind) && wasOpen)
        {
            CloseProject();
        }
    }

    /// <summary>
    /// Gives a project card its Close, Archive and Reopen, and its closure; true when it is put away, so the board leaves it
    /// out of the tabs, the waits count and the overnight queue.
    /// </summary>
    private bool WireClosure(ProjectCardViewModel card, string name)
    {
        card.CloseCommand ??= new RelayCommand(_ => PutProjectAway(name, ProjectClosureKind.Closed), _ => card.IsOpenProject);
        card.ArchiveCommand ??= new RelayCommand(_ => PutProjectAway(name, ProjectClosureKind.Archived), _ => card.IsOpenProject);
        card.ReopenCommand ??= new RelayCommand(_ => Closer.Reopen(name, card.Title), _ => card.IsPutAway);
        card.Closure = ProjectClosures.Of(_settings.Read(), name);

        if (card.Closure is null)
        {
            return false;
        }

        if (card.Waiting.Count > 0)
        {
            card.Waiting = [];
        }

        return true;
    }

    /// <summary>Archives the cards of each archived project whose sessions have ended since the last refresh.</summary>
    private void ArchiveDueCards(IReadOnlyList<ProjectCardViewModel> projects)
    {
        foreach (var project in projects.Where(project => project.Closure is { Kind: ProjectClosureKind.Archived }))
        {
            Closer.ArchiveDue(project.Name, project.MemberKeys);
        }
    }
}
