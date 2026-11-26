using Theseus.Core.Duties;
using Theseus.Core.Projects;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// Closes, archives and reopens a project, for the owner only (archive-a-project, Nigel 2026-11-26: "there is no way to
/// archive or close a project"). Built on what is there: the project's own Stop asks its running cards to hand over and
/// keeps their run state, duties are paused as Theseus pauses them, so no session can lift the pause, and cards are
/// archived as a card's Archive does, once their session has ended. The project's records are never touched: only the
/// settings say it is closed, with what the close changed, so Reopen undoes exactly that and starts nothing.
/// </summary>
public sealed class ProjectCloser
{
    private readonly ISettingsStore _settings;
    private readonly Func<string, WorkstreamCardViewModel?> _cardOf;
    private readonly Action<ProjectCardViewModel> _stop;
    private readonly DutyBook? _duties;
    private readonly Action<WorkstreamCardViewModel> _archive;
    private readonly Action<IReadOnlyCollection<string>> _restore;
    private readonly Func<string, string, string, bool> _confirm;
    private readonly Action<string> _notice;
    private readonly Func<DateTimeOffset> _now;

    /// <summary>Creates the closer.</summary>
    /// <param name="settings">Where closures are kept.</param>
    /// <param name="cardOf">The board card with a key, or null.</param>
    /// <param name="stop">The project's Stop: records its run state and asks its running cards to hand over.</param>
    /// <param name="duties">The duties, or null when there are none.</param>
    /// <param name="archive">Archives a card, as its own Archive does.</param>
    /// <param name="restore">Restores archived cards by key, whether or not the archive is showing.</param>
    /// <param name="confirm">Asks once before a close: caption, question and the action button; true to go on.</param>
    /// <param name="notice">The status bar.</param>
    /// <param name="now">The time now.</param>
    public ProjectCloser(ISettingsStore settings, Func<string, WorkstreamCardViewModel?> cardOf, Action<ProjectCardViewModel> stop,
        DutyBook? duties, Action<WorkstreamCardViewModel> archive, Action<IReadOnlyCollection<string>> restore,
        Func<string, string, string, bool> confirm,
        Action<string> notice, Func<DateTimeOffset> now)
    {
        _settings = settings;
        _cardOf = cardOf;
        _stop = stop;
        _duties = duties;
        _archive = archive;
        _restore = restore;
        _confirm = confirm;
        _notice = notice;
        _now = now;
    }

    /// <summary>Whether a card has a session open; the board's own answer unless a test says otherwise.</summary>
    public Func<WorkstreamCardViewModel, bool> HasSession { get; init; } = card => card.IsLive;

    /// <summary>
    /// The keys of a project's cards that no row of its file names, such as its verifier cards, by project name: they are
    /// stopped and archived with it. Null adds none.
    /// </summary>
    public Func<string, IReadOnlyList<string>>? OthersOf { get; init; }

    /// <summary>Asks cards' sessions to hand over and stop, as a card's Stop does: for the others the project's Stop does not cover.</summary>
    public Action<IReadOnlyList<WorkstreamCardViewModel>>? StopCards { get; init; }

    /// <summary>Called when a project was closed or reopened, so the board shows it at once; null does nothing.</summary>
    public Action? Changed { get; init; }

    /// <summary>What closing or archiving a project would do, as its confirmation says it.</summary>
    public ProjectClose Plan(ProjectCardViewModel project, ProjectClosureKind kind)
    {
        ArgumentNullException.ThrowIfNull(project);

        var running = project.StopPlan?.Keys ?? [];
        var members = Members(project.Name, project.MemberKeys);
        var cards = Cards(members, running);

        return ProjectClose.For(kind, project.Title, cards, DutiesToPause(members).Count,
            _settings.Read().Duties.QueueIsOn(project.Name));
    }

    /// <summary>
    /// Close or Archive on a project: asks once, then stops its running cards as its Stop does, pauses its duties and its
    /// overnight queue, and puts it away; an archive archives its quiet cards now and the rest as their sessions end.
    /// False when the owner cancelled.
    /// </summary>
    public bool Close(ProjectCardViewModel project, ProjectClosureKind kind)
    {
        ArgumentNullException.ThrowIfNull(project);

        var plan = Plan(project, kind);

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
        var reason = kind == ProjectClosureKind.Archived ? "its project is archived" : "its project is closed";

        foreach (var duty in DutiesToPause(members))
        {
            if (_duties!.Pause(duty.Id, Duty.Theseus, reason) is { PausedBy: Duty.Theseus })
            {
                paused.Add(duty.Id);
            }
        }

        _settings.Write(ProjectClosures.Close(_settings.Read(), project.Name,
            new ProjectClosure(kind, _now()) { PausedDuties = paused }));

        ArchiveDue(project.Name, project.MemberKeys);

        var word = kind == ProjectClosureKind.Archived ? "Archived" : "Closed";
        _notice($"{word} {project.Title}. It is under Archived on the board until you reopen it.");
        Changed?.Invoke();

        return true;
    }

    /// <summary>
    /// Reopen: the project is back on the board, its duties and its queue as they were, and the cards the archive
    /// archived restored. Nothing starts: its Start brings back what was running. False when it was not closed.
    /// </summary>
    public bool Reopen(string name, string title)
    {
        ArgumentNullException.ThrowIfNull(name);

        var (settings, closure) = ProjectClosures.Reopen(_settings.Read(), name);

        if (closure is null)
        {
            return false;
        }

        _settings.Write(settings);

        foreach (var id in closure.PausedDuties)
        {
            if (_duties?.Get(id) is { PausedBy: Duty.Theseus })
            {
                _duties.Resume(id);
            }
        }

        if (closure.ArchivedCards.Count > 0)
        {
            _restore(closure.ArchivedCards);
        }

        _notice($"Reopened {title}. Start brings back the cards that were running when it was closed.");
        Changed?.Invoke();

        return true;
    }

    /// <summary>
    /// Archives the cards of an archived project whose sessions have ended, and records them for Reopen. Run on each
    /// refresh, so a card that was handing over is archived once it has.
    /// </summary>
    public void ArchiveDue(string name, IReadOnlyList<string> memberKeys)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(memberKeys);

        var closure = ProjectClosures.Of(_settings.Read(), name);

        if (closure is not { Kind: ProjectClosureKind.Archived })
        {
            return;
        }

        var due = ProjectClosures.DueToArchive(closure, Cards(Members(name, memberKeys), []));

        if (due.Count == 0)
        {
            return;
        }

        foreach (var card in due.Select(_cardOf).OfType<WorkstreamCardViewModel>())
        {
            _archive(card);
        }

        _settings.Write(ProjectClosures.Archived(_settings.Read(), name, due));
    }

    private List<string> Members(string name, IReadOnlyList<string> keys) =>
        [.. keys.Concat(OthersOf?.Invoke(name) ?? []).Distinct(StringComparer.OrdinalIgnoreCase)];

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
