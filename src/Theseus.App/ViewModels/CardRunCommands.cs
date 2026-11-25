using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Core.Settings;

namespace Theseus.App.ViewModels;

/// <summary>
/// What a card's and a selection's Start and Stop do once projects own what runs (1.1e AC 10 to 12), apart from the
/// shell so the wiring can be tested (review of PR 2, finding 4): a card of a running project is asked about first and,
/// stopped, marked stopped by you; ▶ on a card of a stopped project starts the project; ▶ on a done card is Restart for
/// more work, which asks for the instruction; a start of a card takes it off the stopped-by-you list and out of a run
/// state.
/// </summary>
public sealed class CardRunCommands
{
    private readonly ISettingsStore _settings;
    private readonly Func<IReadOnlyList<ProjectCardViewModel>> _projects;
    private readonly ProjectRunner _runner;
    private readonly Func<string, string, bool>? _confirm;
    private readonly Func<string, string, string, string?>? _askText;
    private readonly Action<IReadOnlyList<Workstream>, string> _start;
    private readonly Action<IReadOnlyList<WorkstreamCardViewModel>> _stop;

    /// <summary>Creates the commands.</summary>
    /// <param name="settings">Where the stopped-by-you list and run states are kept.</param>
    /// <param name="projects">The project cards on the board.</param>
    /// <param name="runner">Starts and stops whole projects.</param>
    /// <param name="confirm">Asks the question and the action's label; true to go on. Null goes on with nothing.</param>
    /// <param name="askText">Asks for a line: title, label, initial text; null when cancelled.</param>
    /// <param name="start">The shell's start, with what asked for it.</param>
    /// <param name="stop">The shell's stop of cards' sessions.</param>
    public CardRunCommands(ISettingsStore settings, Func<IReadOnlyList<ProjectCardViewModel>> projects, ProjectRunner runner,
        Func<string, string, bool>? confirm, Func<string, string, string, string?>? askText,
        Action<IReadOnlyList<Workstream>, string> start, Action<IReadOnlyList<WorkstreamCardViewModel>> stop)
    {
        _settings = settings;
        _projects = projects;
        _runner = runner;
        _confirm = confirm;
        _askText = askText;
        _start = start;
        _stop = stop;
    }

    /// <summary>A card's own ■, or Pause on a live card: asks first in a running project, and marks it stopped by you.</summary>
    /// <returns>Whether to go on stopping it.</returns>
    public bool StopCard(WorkstreamCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (!Allowed([card], "Stop"))
        {
            return false;
        }

        MarkStoppedByOwner([card]);

        return true;
    }

    /// <summary>
    /// Stop selected: asks about the selected live cards no selected project covers, marks them stopped by you, records each
    /// selected project's run state, and stops the cards and the projects' sessions, each once.
    /// </summary>
    public void StopSelected(IReadOnlyList<WorkstreamCardViewModel> chosen, IReadOnlyList<ProjectCardViewModel> projects)
    {
        ArgumentNullException.ThrowIfNull(chosen);
        ArgumentNullException.ThrowIfNull(projects);

        var covered = Covered(projects);
        var own = chosen.Where(card => card.IsLive && !covered.Contains(card.Key)).ToList();

        if (!Allowed(own, "Stop"))
        {
            return;
        }

        MarkStoppedByOwner(own);
        _runner.Remember(projects);

        if (_runner.StopSelected(chosen, projects) is { Count: > 0 } cards)
        {
            _stop(cards);
        }
    }

    /// <summary>
    /// A card's own ▶: on a card of a stopped project, the project's Start; on a done ad hoc card, Restart for more work; otherwise
    /// the card's start, asked about first in a running project.
    /// </summary>
    public void StartCard(WorkstreamCardViewModel card)
    {
        ArgumentNullException.ThrowIfNull(card);

        if (card.StopReason?.Kind == WhyStoppedKind.ProjectStopped
            && _projects().FirstOrDefault(project => project.RunState?.Contains(card.Key, StringComparer.OrdinalIgnoreCase) == true)
                is { } stopped)
        {
            _runner.Start(stopped);

            return;
        }

        if (!Allowed([card], "Start"))
        {
            return;
        }

        // In a project sessions restart done cards, so the owner's ▶ is a plain start (sessions-run-the-project item 17).
        if (card.StopReason?.Kind == WhyStoppedKind.Done && card.Workstream.Project is null)
        {
            var more = _askText?.Invoke("Restart for more work", $"What more work is there for {card.Title}?", string.Empty);

            if (!string.IsNullOrWhiteSpace(more))
            {
                _start([card.Workstream with { StartInstruction = more.Trim() }], "RestartForMoreWork");
            }

            return;
        }

        _start([card.Workstream], "StartCard");
    }

    /// <summary>
    /// Start selected: each selected project as a whole, and the selected cards no selected project covers, asked about
    /// first when one is in a running project.
    /// </summary>
    public void StartSelected(IReadOnlyList<WorkstreamCardViewModel> cards, IReadOnlyList<Workstream> startable,
        IReadOnlyList<ProjectCardViewModel> projects)
    {
        ArgumentNullException.ThrowIfNull(cards);
        ArgumentNullException.ThrowIfNull(startable);
        ArgumentNullException.ThrowIfNull(projects);

        var covered = Covered(projects);

        if (Allowed([.. cards.Where(card => !covered.Contains(card.Key))], "Start"))
        {
            _runner.StartSelected(startable, projects);
        }
    }

    /// <summary>Whether to go on: true unless a card is in a running project and the owner chose Cancel (AC 10).</summary>
    public bool Allowed(IReadOnlyList<WorkstreamCardViewModel> cards, string verb) =>
        ProjectCardGuard.Question(cards, _projects(), verb) is not { } question || _confirm?.Invoke(question, verb) == true;

    /// <summary>After a card has launched: off the stopped-by-you list, and out of any run state (AC 9, 12).</summary>
    public void Started(string key)
    {
        var settings = _settings.Read();

        if (settings.CardsStoppedByOwner.Contains(key, StringComparer.OrdinalIgnoreCase))
        {
            _settings.Write(settings with
            {
                CardsStoppedByOwner =
                    [.. settings.CardsStoppedByOwner.Where(other => !string.Equals(other, key, StringComparison.OrdinalIgnoreCase))],
            });
        }

        ProjectRunner.Started(_settings, [key]);
    }

    private void MarkStoppedByOwner(IReadOnlyList<WorkstreamCardViewModel> cards)
    {
        var caught = cards.Where(card => card.IsLive && ProjectCardGuard.Question([card], _projects(), "Stop") is not null)
            .Select(card => card.Key)
            .ToList();

        if (caught.Count > 0)
        {
            var settings = _settings.Read();
            _settings.Write(settings with
            {
                CardsStoppedByOwner = [.. settings.CardsStoppedByOwner.Union(caught, StringComparer.OrdinalIgnoreCase)],
            });
        }
    }

    private static HashSet<string> Covered(IReadOnlyList<ProjectCardViewModel> projects) =>
        projects.SelectMany(project => project.MemberKeys).ToHashSet(StringComparer.OrdinalIgnoreCase);
}
