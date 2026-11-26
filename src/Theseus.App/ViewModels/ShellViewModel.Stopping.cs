using Theseus.Core.Locks;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// Stop the project and Start the project (project-view-as-tabs, Nigel 2026-11-26). They are on the project card's ▶ and ■,
/// the project's tab, the toolbar and the Project menu. They are the owner's alone: no session tool reaches them.
/// </summary>
public sealed partial class ShellViewModel
{
    /// <summary>The machine's lock book, so Stop the project can release a stopped card's holds; null releases none.</summary>
    public LockBook? LockBook { get; set; }

    private ProjectStopper Stopper => new(_settings, Board.Card, project => Runner.Stop(project), Duties,
        (caption, question, action) => ConfirmProjectClose?.Invoke(caption, question, action) == true,
        text => Notice = text, () => DateTimeOffset.UtcNow)
    {
        Changed = Refresh,
        OthersOf = name => [.. Board.Cards.Where(card => string.Equals(card.Workstream.Project, name, StringComparison.OrdinalIgnoreCase))
            .Select(card => card.Key)],
        StopCards = cards => BeginStop(StepsFor(cards)),
        Tickets = () => LockBook?.Snapshot() ?? [],
        Release = (card, session) => LockBook is { } book ? CardLocks.Release(book, card, session) : 0,
    };

    /// <summary>
    /// Gives a project card its ▶ and ■ as Start the project and Stop the project, before the runner's own: ■ asks once and
    /// stops, pauses and releases; ▶ resumes what the stop paused, then starts the run state as before.
    /// </summary>
    private void WireStopping(ProjectCardViewModel card, Func<bool> canLaunch)
    {
        card.StopCommand ??= new RelayCommand(_ => Stopper.Stop(card), _ => Stopper.CanStop(card));
        card.StartCommand ??= new RelayCommand(_ =>
            {
                Stopper.Resume(card.Name);

                if (card.StartPlan is { CanStart: true } && canLaunch())
                {
                    Runner.Start(card);
                }
            },
            _ => (card.StartPlan is { CanStart: true } && canLaunch()) || ProjectStops.Of(_settings.Read(), card.Name) is not null);
    }

    /// <summary>Releases the locks of each stopped project's cards whose sessions have ended since the last refresh.</summary>
    private void ReleaseDueLocks(IReadOnlyList<ProjectCardViewModel> projects)
    {
        if (_settings.Read().StoppedProjects.Count == 0)
        {
            return;
        }

        var stopper = Stopper;

        foreach (var project in projects)
        {
            stopper.ReleaseDue(project.Name, project.MemberKeys);
        }
    }
}
