using Theseus.Core.Domain;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// Starts and stops a whole project (1.1e AC 8, 9, 11; Nigel, 2026-11-21: "I might ask the project to stop if I am going
/// to turn off my machine ... and when I restart the project, it would go back to the previous state"). Stop records the
/// project's run state, the cards with a session at that moment, then asks each to stop; Start starts exactly that run
/// state, or a never-stopped project's first card, with no list to tick. A card leaves the run state once it has really
/// launched (<see cref="Started"/>) or turned out unable to start, so a start that is cancelled or refused loses nothing
/// (review of PR 2, finding 1). The run state is kept in the settings, so it survives a restart of Theseus and of the
/// machine. The shell gives it its own start and stop, so what runs is exactly what a card's buttons run.
/// </summary>
public sealed class ProjectRunner
{
    private readonly Func<string, WorkstreamCardViewModel?> _cardOf;
    private readonly ISettingsStore _settings;
    private readonly Action<IReadOnlyList<Workstream>> _start;
    private readonly Action<IReadOnlyList<WorkstreamCardViewModel>> _stop;
    private readonly Action<string> _notice;
    private readonly Action? _changed;

    /// <summary>Creates the runner.</summary>
    /// <param name="cardOf">The board card with a key, or null.</param>
    /// <param name="settings">Where run states are kept.</param>
    /// <param name="start">Starts workstreams, as a card's Start does.</param>
    /// <param name="stop">Asks cards' sessions to write a handover and stop, as a card's Stop does.</param>
    /// <param name="notice">Says in the status bar which cards a Start skipped, and why.</param>
    /// <param name="changed">Called when a run state changed, so the board shows it at once; null does nothing.</param>
    public ProjectRunner(Func<string, WorkstreamCardViewModel?> cardOf, ISettingsStore settings,
        Action<IReadOnlyList<Workstream>> start, Action<IReadOnlyList<WorkstreamCardViewModel>> stop, Action<string> notice,
        Action? changed = null)
    {
        _changed = changed;
        _cardOf = cardOf;
        _settings = settings;
        _start = start;
        _stop = stop;
        _notice = notice;
    }

    /// <summary>▶ on a project card, or Start on its menu.</summary>
    public void Start(ProjectCardViewModel project)
    {
        ArgumentNullException.ThrowIfNull(project);

        if (project.StartPlan is { CanStart: false, Skipped.Count: 0 } nothing)
        {
            _notice(nothing.ToolTip);

            return;
        }

        StartSelected([], [project]);
    }

    /// <summary>
    /// Start selected (1.1e AC 11): each selected project as a whole, and the selected cards no selected project covers, so
    /// a card in both is started once, through its project. A run state's cards that cannot start now leave it at once;
    /// the rest leave it as they launch. A project with nothing to start says why.
    /// </summary>
    public void StartSelected(IReadOnlyList<Workstream> cards, IReadOnlyList<ProjectCardViewModel> projects)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(projects);

        var planned = projects.Where(project => project.StartPlan is not null).ToList();
        var chosen = ChooseSelected(cards, projects);
        var skipped = planned.SelectMany(project => project.StartPlan!.Skipped).ToList();
        var unstartable = planned.Where(project => project.IsStopped)
            .SelectMany(project => project.StartPlan!.Entries.Where(entry => !entry.Starts).Select(entry => entry.Key))
            .ToList();

        if (unstartable.Count > 0 && Started(_settings, unstartable))
        {
            _changed?.Invoke();
        }

        if (chosen.Count > 0)
        {
            _start(chosen);
        }

        if (skipped.Count > 0)
        {
            _notice($"Not started: {string.Join("; ", skipped)}.");
        }
    }

    /// <summary>
    /// Takes cards out of every run state, once they have launched or cannot start (review of PR 2, findings 1 and 2); a run
    /// state left empty is removed, so its project is no longer stopped. True when anything changed.
    /// </summary>
    public static bool Started(ISettingsStore settings, IReadOnlyCollection<string> keys)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(keys);

        var read = settings.Read();
        var states = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var (project, state) in read.ProjectRunStates)
        {
            var left = state.Where(key => !keys.Contains(key, StringComparer.OrdinalIgnoreCase)).ToList();
            changed |= left.Count != state.Count;

            if (left.Count > 0)
            {
                states[project] = left;
            }
        }

        if (changed)
        {
            settings.Write(read with { ProjectRunStates = states });
        }

        return changed;
    }

    /// <summary>What Start selected starts: the selected cards no selected project covers, then each project's cards.</summary>
    public IReadOnlyList<Workstream> ChooseSelected(IReadOnlyList<Workstream> cards, IReadOnlyList<ProjectCardViewModel> projects)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(projects);

        var covered = Covered(projects);

        return
        [
            .. cards.Where(card => !covered.Contains(card.Key))
                .Concat(projects.Where(project => project.StartPlan is { CanStart: true })
                    .SelectMany(project => project.StartPlan!.Keys)
                    .Select(_cardOf)
                    .OfType<WorkstreamCardViewModel>()
                    .Select(card => card.Workstream))
                .DistinctBy(workstream => workstream.Key, StringComparer.OrdinalIgnoreCase),
        ];
    }

    /// <summary>■ on a project card, or Stop on its menu: records its run state, then stops its sessions.</summary>
    public void Stop(ProjectCardViewModel project)
    {
        ArgumentNullException.ThrowIfNull(project);

        Remember([project]);

        if (StopSelected([], [project]) is { Count: > 0 } cards)
        {
            _stop(cards);
        }
    }

    /// <summary>
    /// What Stop selected stops (1.1e AC 11): the selected cards no selected project covers, and each selected project's
    /// cards with a session open, each once. It records nothing: <see cref="Remember"/> does, when the stop is made.
    /// </summary>
    public IReadOnlyList<WorkstreamCardViewModel> StopSelected(IReadOnlyList<WorkstreamCardViewModel> cards,
        IReadOnlyList<ProjectCardViewModel> projects)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(projects);

        var covered = Covered(projects);

        return
        [
            .. cards.Where(card => !covered.Contains(card.Key))
                .Concat(projects.SelectMany(project => project.StopPlan?.Keys ?? []).Select(_cardOf).OfType<WorkstreamCardViewModel>())
                .Distinct(),
        ];
    }

    /// <summary>
    /// Records the run state of each of these projects that has a session to stop (1.1e AC 8), added to any run state it
    /// already has, so a card started by hand in between and stopped again does not lose the others (review, finding 2).
    /// </summary>
    public void Remember(IReadOnlyList<ProjectCardViewModel> projects)
    {
        ArgumentNullException.ThrowIfNull(projects);

        var stopping = projects.Where(project => project.StopPlan is { CanStop: true }).ToList();

        if (stopping.Count == 0)
        {
            return;
        }

        var settings = _settings.Read();
        var states = new Dictionary<string, IReadOnlyList<string>>(settings.ProjectRunStates, StringComparer.OrdinalIgnoreCase);

        foreach (var project in stopping)
        {
            states[project.Name] = [.. (states.GetValueOrDefault(project.Name) ?? []).Union(project.StopPlan!.Keys, StringComparer.OrdinalIgnoreCase)];
        }

        _settings.Write(settings with { ProjectRunStates = states });
        _changed?.Invoke();
    }

    private static HashSet<string> Covered(IReadOnlyList<ProjectCardViewModel> projects) =>
        projects.SelectMany(project => project.MemberKeys).ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>Gives a project card its ▶ and ■, enabled from its plans; Start also needs Theseus able to launch.</summary>
    public void Wire(ProjectCardViewModel card, Func<bool> canLaunch)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canLaunch);

        card.StartCommand ??= new RelayCommand(_ => Start(card), _ => card.StartPlan is { CanStart: true } && canLaunch());
        card.StopCommand ??= new RelayCommand(_ => Stop(card), _ => card.StopPlan is { CanStop: true });
    }
}
