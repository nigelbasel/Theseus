using Theseus.Core.Locks;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// Stop the project and Start the project (project-view-as-tabs, Nigel 2026-11-26). They are on the project card's ▶ and ■,
/// the project's tab, the toolbar and the Project menu. They are the owner's alone: no session tool reaches them.
/// </summary>
public sealed partial class ShellViewModel
{
    private HashSet<string> _stopped = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _canStop = new(StringComparer.OrdinalIgnoreCase);

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
        Release = (card, session) => LockBook is { } book ? CardLocks.Release(book, card, session, id => Sessions.Find(id) is not null) : 0,
    };

    /// <summary>
    /// Gives a project card its ▶ and ■ as Start the project and Stop the project, before the runner's own. ■ asks once,
    /// then stops, pauses and releases. ▶ resumes what the stop paused, then starts the run state as before (the runner
    /// resumes on every Start path). Whether each can run is worked out once per refresh, not on every requery: it reads
    /// the settings and the lock book.
    /// </summary>
    private void WireStopping(ProjectCardViewModel card, Func<bool> canLaunch)
    {
        card.StopCommand ??= new RelayCommand(_ => Stopper.Stop(card), _ => _canStop.Contains(card.Name));
        card.StartCommand ??= new RelayCommand(_ => Runner.Start(card),
            _ => (card.StartPlan is { CanStart: true } && canLaunch()) || _stopped.Contains(card.Name));
    }

    /// <summary>
    /// Once per refresh: releases the locks of each stopped project's cards whose sessions have ended, and works out which
    /// projects are stopped and which have anything to stop.
    /// </summary>
    private void RefreshStopping(IReadOnlyList<ProjectCardViewModel> projects)
    {
        var stopper = Stopper;
        var settings = _settings.Read();

        if (settings.StoppedProjects.Count > 0)
        {
            foreach (var project in projects)
            {
                stopper.ReleaseDue(project.Name, project.MemberKeys);
            }

            settings = _settings.Read();
        }

        _stopped = [.. projects.Where(project => ProjectStops.IsStopped(settings, project.Name)).Select(project => project.Name)];
        _canStop = [.. projects.Where(stopper.CanStop).Select(project => project.Name)];
        System.Windows.Input.CommandManager.InvalidateRequerySuggested();
    }
}
