using Theseus.Core.Duties;
using Theseus.Core.Locks;
using Theseus.Core.Projects;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// Stop the project and its Start, for the owner only (project-view-as-tabs, Nigel 2026-11-26: "there is no way to tell the
/// project to stop and tell all its workstreams to stop and release their locks"). It asks once. Then it stops the running
/// cards through the project's own Stop, so their run state is kept, and pauses the project's duties as Theseus pauses
/// them, so no session lifts the pause, along with its overnight queue. Once each card's session has ended, it releases
/// that card's session holds and queue places. A process hold is never released, since it ends with its run. The project
/// stays open, and Start the project resumes exactly what the stop paused. The project card's ■ is this command (Nigel,
/// 2026-11-26).
/// </summary>
public sealed class ProjectStopper
{
    private readonly ISettingsStore _settings;
    private readonly Func<string, WorkstreamCardViewModel?> _cardOf;
    private readonly Action<ProjectCardViewModel> _stop;
    private readonly DutyBook? _duties;
    private readonly Func<string, string, string, bool> _confirm;
    private readonly Action<string> _notice;
    private readonly Func<DateTimeOffset> _now;

    /// <summary>Creates the stopper.</summary>
    /// <param name="settings">Where stops are kept.</param>
    /// <param name="cardOf">The board card with a key, or null.</param>
    /// <param name="stop">The project's run-state Stop: records its run state and asks its running cards to hand over.</param>
    /// <param name="duties">The duties, or null when there are none.</param>
    /// <param name="confirm">Asks once: caption, question and the action button; true to go on.</param>
    /// <param name="notice">The status bar.</param>
    /// <param name="now">The time now.</param>
    public ProjectStopper(ISettingsStore settings, Func<string, WorkstreamCardViewModel?> cardOf, Action<ProjectCardViewModel> stop,
        DutyBook? duties, Func<string, string, string, bool> confirm, Action<string> notice, Func<DateTimeOffset> now)
    {
        _settings = settings;
        _cardOf = cardOf;
        _stop = stop;
        _duties = duties;
        _confirm = confirm;
        _notice = notice;
        _now = now;
    }

    /// <summary>Whether a card has a session open; the board's own answer unless a test says otherwise.</summary>
    public Func<WorkstreamCardViewModel, bool> HasSession { get; init; } = card => card.IsLive;

    /// <summary>The keys of a project's cards no row of its file names, such as its verifier cards; null adds none.</summary>
    public Func<string, IReadOnlyList<string>>? OthersOf { get; init; }

    /// <summary>Asks cards' sessions to hand over and stop: for the others the project's Stop does not cover.</summary>
    public Action<IReadOnlyList<WorkstreamCardViewModel>>? StopCards { get; init; }

    /// <summary>Every lock ticket now; none when there is no lock book.</summary>
    public Func<IReadOnlyList<LockTicket>> Tickets { get; init; } = () => [];

    /// <summary>Releases a stopped card's releasable tickets, by key and session; how many ended.</summary>
    public Func<string, string?, int> Release { get; init; } = (_, _) => 0;

    /// <summary>Called when a project was stopped or started, so the board shows it at once; null does nothing.</summary>
    public Action? Changed { get; init; }

    /// <summary>Whether Stop the project has anything to do: a card running, a live duty, the queue on, or a lock to release.</summary>
    public bool CanStop(ProjectCardViewModel project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var members = Members(project.Name, project.MemberKeys);

        return project.StopPlan is { CanStop: true }
               || members.Select(_cardOf).OfType<WorkstreamCardViewModel>().Any(HasSession)
               || DutiesToPause(members).Count > 0
               || _settings.Read().Duties.QueueIsOn(project.Name)
               || Owned(members).Any(CardLocks.Releasable);
    }

    /// <summary>What Stop the project would do, as its confirmation says it.</summary>
    public ProjectStopConfirmation Plan(ProjectCardViewModel project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var running = project.StopPlan?.Keys ?? [];
        var members = Members(project.Name, project.MemberKeys);
        var tickets = Owned(members);

        return ProjectStopConfirmation.For(project.Title, Cards(members, running), DutiesToPause(members).Count,
            _settings.Read().Duties.QueueIsOn(project.Name),
            holds: tickets.Count(ticket => ticket.IsHolder && CardLocks.Releasable(ticket)),
            places: tickets.Count(ticket => !ticket.IsHolder),
            runs: tickets.Count(ticket => ticket.IsHolder && !CardLocks.Releasable(ticket)));
    }

    /// <summary>Stop the project: asks once, then stops, pauses and releases as the type says. False when the owner cancelled.</summary>
    public bool Stop(ProjectCardViewModel project)
    {
        ArgumentNullException.ThrowIfNull(project);

        var plan = Plan(project);

        if (!_confirm(plan.Caption, plan.Question, plan.Action))
        {
            return false;
        }

        _stop(project);

        var covered = project.StopPlan?.Keys ?? [];
        var members = Members(project.Name, project.MemberKeys);
        List<WorkstreamCardViewModel> others =
        [
            .. members.Where(key => !covered.Contains(key, StringComparer.OrdinalIgnoreCase))
                .Select(_cardOf).OfType<WorkstreamCardViewModel>().Where(HasSession),
        ];

        if (others.Count > 0)
        {
            StopCards?.Invoke(others);
        }

        var paused = new List<string>();

        foreach (var duty in DutiesToPause(members))
        {
            if (_duties!.Pause(duty.Id, Duty.Theseus, "its project is stopped") is { PausedBy: Duty.Theseus })
            {
                paused.Add(duty.Id);
            }
        }

        _settings.Write(ProjectStops.Stop(_settings.Read(), project.Name,
            new ProjectStopping(_now()) { PausedDuties = paused, PendingLockCards = members }));

        ReleaseDue(project.Name, project.MemberKeys);
        _notice($"Stopping {project.Title}: its cards hand over and stop, and their locks are released as each one does.");
        Changed?.Invoke();

        return true;
    }

    /// <summary>
    /// Releases the locks of each stopped card whose session has ended, once, and takes it off the pending list. Run after
    /// a stop and on each refresh, so a card handing over is released once it has.
    /// </summary>
    public void ReleaseDue(string name, IReadOnlyList<string>? memberKeys = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (ProjectStops.Of(_settings.Read(), name) is not { PendingLockCards.Count: > 0 } stopping)
        {
            return;
        }

        var done = new List<string>();

        foreach (var key in stopping.PendingLockCards)
        {
            var card = _cardOf(key);

            if (card is not null && HasSession(card))
            {
                continue;
            }

            if (CardLocks.Of(Tickets(), key, card?.Workstream.LastSessionId).Any(CardLocks.Releasable))
            {
                Release(key, card?.Workstream.LastSessionId);
            }

            done.Add(key);
        }

        if (done.Count > 0)
        {
            _settings.Write(ProjectStops.Released(_settings.Read(), name, done));
        }
    }

    /// <summary>Start the project's half: resumes the duties and queue the stop paused, and no pause the owner set. False when it was not stopped.</summary>
    public bool Resume(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        var (settings, stopping) = ProjectStops.Resume(_settings.Read(), name);

        if (stopping is null)
        {
            return false;
        }

        _settings.Write(settings);

        foreach (var id in stopping.PausedDuties)
        {
            if (_duties?.Get(id) is { PausedBy: Duty.Theseus })
            {
                _duties.Resume(id);
            }
        }

        Changed?.Invoke();

        return true;
    }

    private List<string> Members(string name, IReadOnlyList<string> keys) =>
        [.. keys.Concat(OthersOf?.Invoke(name) ?? []).Distinct(StringComparer.OrdinalIgnoreCase)];

    private List<LockTicket> Owned(IReadOnlyList<string> members)
    {
        var tickets = Tickets();

        return
        [
            .. members.SelectMany(key => CardLocks.Of(tickets, key, _cardOf(key)?.Workstream.LastSessionId))
                .DistinctBy(ticket => ticket.Token, StringComparer.Ordinal),
        ];
    }

    private List<ProjectCloseCard> Cards(IReadOnlyList<string> keys, IReadOnlyList<string> running) =>
    [
        .. keys.Select(_cardOf).OfType<WorkstreamCardViewModel>()
            .Select(card => new ProjectCloseCard(card.Key, card.Title,
                HasSession(card) || running.Contains(card.Key, StringComparer.OrdinalIgnoreCase), card.IsArchived)),
    ];

    private List<Duty> DutiesToPause(IReadOnlyList<string> keys) =>
        _duties is null
            ? []
            : [.. _duties.Live.Where(duty => !duty.IsPaused && keys.Contains(duty.Card, StringComparer.OrdinalIgnoreCase))];
}
