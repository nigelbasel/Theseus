using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Projects;
using Theseus.Core.Sessions;

namespace Theseus.App.ViewModels;

/// <summary>
/// Starts and stops a whole project as its cards' own Start and Stop do (1.1d AC 43): Start asks first, listing the
/// project's cards with tick boxes, and starts the ticked ones in the project's terminal window; Stop asks each of its
/// running sessions to write its handover and stop. The shell gives it its own start and stop, so what runs is exactly
/// what a card's buttons run. Nothing on the board changes until something is started: a paused card ticked in the
/// confirmation goes to the start with its mark off, and the mark is taken off its card only once it has started
/// (<see cref="StartedMarks"/>).
/// </summary>
public sealed class ProjectRunner
{
    private readonly Func<string, WorkstreamCardViewModel?> _cardOf;
    private readonly Func<ProjectStartViewModel, bool> _confirm;
    private readonly Func<WorkstreamCardViewModel, string?> _refusal;
    private readonly Action<IReadOnlyList<Workstream>> _start;
    private readonly Action<IReadOnlyList<WorkstreamCardViewModel>> _stop;
    private readonly Action<string, bool> _notice;
    private readonly List<string> _refused = [];

    /// <summary>Creates the runner.</summary>
    /// <param name="cardOf">The board card with a key, or null.</param>
    /// <param name="confirm">Shows the confirmation; true when Start was pressed.</param>
    /// <param name="refusal">Why a card cannot start now, or null (<see cref="RefusalOf"/>, against the live sessions).</param>
    /// <param name="start">Starts workstreams, as a card's Start does.</param>
    /// <param name="stop">Asks cards' sessions to write a handover and stop, as a card's Stop does.</param>
    /// <param name="notice">
    /// Says in the status bar which ticked cards were not started, and why: added to what the start said (true), or alone
    /// when nothing started (false).
    /// </param>
    public ProjectRunner(Func<string, WorkstreamCardViewModel?> cardOf, Func<ProjectStartViewModel, bool> confirm,
        Func<WorkstreamCardViewModel, string?> refusal, Action<IReadOnlyList<Workstream>> start,
        Action<IReadOnlyList<WorkstreamCardViewModel>> stop, Action<string, bool> notice)
    {
        _cardOf = cardOf;
        _confirm = confirm;
        _refusal = refusal;
        _start = start;
        _stop = stop;
        _notice = notice;
    }

    /// <summary>
    /// Why a card cannot be started at the moment Start is pressed, or null: the list was drawn at the last refresh, and
    /// a session may have started or the card been blocked since (review of PR 3, item 2).
    /// </summary>
    public static string? RefusalOf(WorkstreamCardViewModel card, SessionRegistrySnapshot live)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(live);

        if (card.IsBlocked)
        {
            return "is blocked";
        }

        if (StartSelection.IsEligible(card.Workstream, live))
        {
            return null;
        }

        return !string.IsNullOrWhiteSpace(card.Workstream.LastSessionId) && live.WithSessionId(card.Workstream.LastSessionId).Count > 0
            ? "is running"
            : "cannot be started from the board";
    }

    /// <summary>
    /// Asks which of a project's cards to start, and returns them, or null when the confirmation was cancelled. A paused
    /// card ticked there is returned with its mark off: ticking it was the answer to the question a paused card's Start
    /// asks (rubric R13), so it is not asked twice. A ticked card that can no longer start is left out and named.
    /// </summary>
    public IReadOnlyList<Workstream>? Choose(ProjectStart plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var question = new ProjectStartViewModel(plan);

        if (!plan.CanStart || !_confirm(question))
        {
            return null;
        }

        var ticked = question.TickedKeys.Select(_cardOf).OfType<WorkstreamCardViewModel>().ToList();
        var refused = ticked.Select(card => (Card: card, Reason: _refusal(card))).Where(each => each.Reason is not null).ToList();

        _refused.AddRange(refused.Select(each => $"{each.Card.Title} {each.Reason}"));

        return
        [
            .. ticked.Except(refused.Select(each => each.Card))
                .Select(card => card.IsPaused ? card.Workstream with { IsPaused = false } : card.Workstream),
        ];
    }

    /// <summary>▶ on a project card, or Start… on its menu.</summary>
    public void Start(ProjectStart plan)
    {
        _refused.Clear();

        if (Choose(plan) is not { } chosen)
        {
            return;
        }

        if (chosen.Count > 0)
        {
            _start(chosen);
        }

        SayRefused(started: chosen.Count > 0);
    }

    /// <summary>Start selected, through the board's start, saying which ticked cards were left out.</summary>
    public void StartSelected(IReadOnlyList<Workstream> cards, IReadOnlyList<ProjectCardViewModel> projects)
    {
        if (ChooseSelected(cards, projects) is not { } chosen)
        {
            return;
        }

        if (chosen.Count > 0)
        {
            _start(chosen);
        }

        SayRefused(started: chosen.Count > 0);
    }

    private void SayRefused(bool started)
    {
        if (_refused.Count > 0)
        {
            _notice($"Not started, since the board changed after the list was shown: {string.Join("; ", _refused)}.", started);
        }
    }

    /// <summary>
    /// Start selected (1.1d AC 43): the cards that can start, then for each selected project that can start the cards its
    /// confirmation ticks. A card ticked both on the board and in a project's confirmation is started once, as the
    /// confirmation gave it, so a paused one is not asked about again (review of PR 3, item 4). Null when any
    /// confirmation was cancelled, so nothing at all starts and nothing changes.
    /// </summary>
    public IReadOnlyList<Workstream>? ChooseSelected(IReadOnlyList<Workstream> cards, IReadOnlyList<ProjectCardViewModel> projects)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(projects);

        _refused.Clear();
        var chosen = cards.ToList();

        foreach (var project in projects.Where(project => project.StartPlan is { CanStart: true }))
        {
            if (Choose(project.StartPlan!) is not { } ticked)
            {
                return null;
            }

            foreach (var workstream in ticked)
            {
                var at = chosen.FindIndex(other => string.Equals(other.Key, workstream.Key, StringComparison.OrdinalIgnoreCase));

                if (at >= 0)
                {
                    chosen[at] = workstream;
                }
                else
                {
                    chosen.Add(workstream);
                }
            }
        }

        return chosen;
    }

    /// <summary>The cards a project's Stop asks, as they are on the board.</summary>
    public IReadOnlyList<WorkstreamCardViewModel> LiveCards(ProjectStop? plan) =>
        plan is null ? [] : [.. plan.Keys.Select(_cardOf).OfType<WorkstreamCardViewModel>()];

    /// <summary>■ on a project card, or Stop on its menu.</summary>
    public void Stop(ProjectStop plan)
    {
        if (LiveCards(plan) is { Count: > 0 } cards)
        {
            _stop(cards);
        }
    }

    /// <summary>Stop selected: the selected cards, and each selected project's cards with a session open, each once.</summary>
    public IReadOnlyList<WorkstreamCardViewModel> StopSelected(IReadOnlyList<WorkstreamCardViewModel> cards,
        IReadOnlyList<ProjectCardViewModel> projects)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(projects);

        return [.. cards.Concat(projects.SelectMany(project => LiveCards(project.StopPlan))).Distinct()];
    }

    /// <summary>Gives a project card its ▶ and ■, enabled from its plans; Start also needs Theseus able to launch.</summary>
    public void Wire(ProjectCardViewModel card, Func<bool> canLaunch)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(canLaunch);

        card.StartCommand ??= new RelayCommand(_ =>
        {
            if (card.StartPlan is { } plan)
            {
                Start(plan);
            }
        }, _ => card.StartPlan is { CanStart: true } && canLaunch());
        card.StopCommand ??= new RelayCommand(_ =>
        {
            if (card.StopPlan is { } plan)
            {
                Stop(plan);
            }
        }, _ => card.StopPlan is { CanStop: true });
    }
}
