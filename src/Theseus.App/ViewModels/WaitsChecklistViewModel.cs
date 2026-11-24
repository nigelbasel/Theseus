using System.Windows.Input;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// The waits checklist (git-22 for Nigel, 2026-11-24: lists of items go in checklist dialogs): every wait as a ticked or
/// unticked group, and a command bar acting on what is ticked. It is the one place the owner acts on a criterion's ask;
/// the band's lines, the objectives' banners and the phases' held lines open it with theirs ticked.
/// </summary>
public sealed class WaitsChecklistViewModel : ObservableObject
{
    private const string By = "owner";

    private readonly WaitsChecklistActions _actions;
    private readonly Func<DateOnly> _today;

    /// <summary>Creates the checklist.</summary>
    /// <param name="rows">Every wait, with whom it is on, in the order shown.</param>
    /// <param name="ticked">The waits it opens with ticked.</param>
    /// <param name="actions">What the command bar runs.</param>
    /// <param name="today">Today, for the decisions it records; the local date when null.</param>
    public WaitsChecklistViewModel(IReadOnlyList<(WaitItemViewModel Line, string Whom)> rows, IEnumerable<WaitItem> ticked,
        WaitsChecklistActions actions, Func<DateOnly>? today = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(actions);

        var start = ticked.ToHashSet();
        _actions = actions;
        _today = today ?? (() => DateOnly.FromDateTime(DateTime.Now));
        Groups = [.. rows.Select(row => new WaitChecklistGroupViewModel(row.Line, row.Whom, start.Contains(row.Line.Item), Recount))];

        AnswerCommand = new RelayCommand(_ => Answer(), _ => WhyNotEdit is null);
        DoneCommand = new RelayCommand(_ => MarkDone(), _ => WhyNotEdit is null);
        ReassignCommand = new RelayCommand(_ => Reassign(), _ => WhyNotEdit is null);
        AskLeadCommand = new RelayCommand(_ => AskLead(), _ => WhyNotAskLead is null);
        GoToObjectiveCommand = new RelayCommand(_ => GoToObjective(), _ => WhyNotGoTo is null);
        SelectAllCommand = new RelayCommand(_ => TickAll(true));
        ClearCommand = new RelayCommand(_ => TickAll(false), _ => Groups.Any(group => group.IsAnyTicked));
    }

    /// <summary>Raised when an action has run, so the dialog closes: the view is rebuilt from the file it wrote.</summary>
    public event EventHandler? Finished;

    /// <summary>The dialog's title.</summary>
    public string Title => $"Waits: {_actions.Project}";

    /// <summary>What the dialog is for, above the list.</summary>
    public string Intro => "Tick the waits to act on, then choose an action. Ticking a wait ticks every criterion it covers.";

    /// <summary>Every wait, grouped by what it asks.</summary>
    public IReadOnlyList<WaitChecklistGroupViewModel> Groups { get; }

    /// <summary>"3 of 47 selected".</summary>
    public string CountText => $"{Groups.Count(group => group.IsAnyTicked)} of {Groups.Count} selected";

    /// <summary>Records one answer for every ticked wait, and takes them off their criteria.</summary>
    public ICommand AnswerCommand { get; }

    /// <summary>Records the ticked waits as done, and takes them off their criteria.</summary>
    public ICommand DoneCommand { get; }

    /// <summary>Moves the ticked waits to someone else, or back to the owner.</summary>
    public ICommand ReassignCommand { get; }

    /// <summary>Gives the ticked waits to the lead's session as its prompt and brings its tab forward.</summary>
    public ICommand AskLeadCommand { get; }

    /// <summary>Shows the one objective the ticked waits concern.</summary>
    public ICommand GoToObjectiveCommand { get; }

    /// <summary>Ticks every wait.</summary>
    public ICommand SelectAllCommand { get; }

    /// <summary>Unticks every wait.</summary>
    public ICommand ClearCommand { get; }

    /// <summary>Answer…'s tooltip: what it does, or why it cannot.</summary>
    public string AnswerToolTip => WhyNotEdit ?? "Record one answer for every ticked wait, as a decision in project.md, and clear them";

    /// <summary>Mark done's tooltip.</summary>
    public string DoneToolTip => WhyNotEdit ?? "Record the ticked waits as done, as a decision in project.md, and clear them";

    /// <summary>Reassign to…'s tooltip.</summary>
    public string ReassignToolTip => WhyNotEdit ?? "Say whom the ticked waits are on now, such as the lead or another team";

    /// <summary>Ask the lead's tooltip.</summary>
    public string AskLeadToolTip => WhyNotAskLead ?? $"Give the ticked waits to {_actions.LeadName} as its prompt, and bring its tab forward";

    /// <summary>Go to objective's tooltip.</summary>
    public string GoToObjectiveToolTip => WhyNotGoTo ?? $"Show objective {TickedObjectives.Single()} in the project view";

    private List<WaitChecklistGroupViewModel> Ticked => [.. Groups.Where(group => group.IsAnyTicked)];

    private List<string> TickedObjectives => [.. Ticked.SelectMany(group => group.TickedObjectives).Distinct(StringComparer.Ordinal)];

    private string? WhyNotEdit =>
        Ticked.Count == 0 ? "Tick at least one wait first."
        : Ticked.Any(group => !group.IsCriterionWait) ? "A ticked wait is not on a criterion; use its own button. Untick it to go on."
        : _actions.EditFile is null || _actions.AskText is null ? "This view cannot write the project file."
        : null;

    private string? WhyNotAskLead =>
        Ticked.Count == 0 ? "Tick at least one wait first."
        : _actions.AskLead is null ? "This project has no lead card on the board."
        : null;

    private string? WhyNotGoTo =>
        TickedObjectives.Count == 1 ? null
        : TickedObjectives.Count == 0 ? "Tick a wait on an objective first."
        : "The ticked waits concern more than one objective; tick one objective's.";

    private void Answer()
    {
        if (_actions.AskText!("Answer", "Your answer, recorded for every ticked wait", string.Empty) is not { Length: > 0 } answer)
        {
            return;
        }

        Write(answer.Trim() + $" (answers: {Asked()})", $"Recorded your answer to {Count()} in {_actions.Project}.");
    }

    private void MarkDone() => Write($"Marked done: {Asked()}", $"Marked {Count()} done in {_actions.Project}.");

    private void Write(string decision, string done)
    {
        var criteria = Ticked.SelectMany(group => group.TickedCriteria).ToList();

        _actions.EditFile!(text => ProjectFileEditor.AddDecision(WaitEdits.Clear(text, criteria), _today(), decision, By), done);
        Finished?.Invoke(this, EventArgs.Empty);
    }

    private void Reassign()
    {
        var label = "Whom the ticked waits are on now; leave it empty for you";
        var who = (string?)null;

        while (true)
        {
            if (_actions.AskText!("Reassign to", label, who ?? _actions.LeadName ?? string.Empty) is not { } whom)
            {
                return;
            }

            who = whom.Trim();

            if (who.Length == 0 || WaitingAsk.IsWritableWho(who))
            {
                break;
            }

            label = $"\"{who}\" cannot be written: use at most {WaitingAsk.LongestWho} characters and no colon or semicolon";
        }

        who = who.Length == 0 || who.Equals("me", StringComparison.OrdinalIgnoreCase) || who.Equals("you", StringComparison.OrdinalIgnoreCase)
              || who.Equals("owner", StringComparison.OrdinalIgnoreCase)
            ? null
            : who;
        var ticked = Ticked;

        _actions.EditFile!(text =>
            {
                foreach (var group in ticked)
                {
                    text = WaitEdits.Reassign(text, group.TickedCriteria, who);
                }

                return ProjectFileEditor.AddDecision(text, _today(), $"Reassigned to {who ?? "the owner"}: {Asked()}", By);
            },
            $"{Count()} now {(ticked.Count == 1 ? "waits" : "wait")} on {who ?? "you"} in {_actions.Project}.");
        Finished?.Invoke(this, EventArgs.Empty);
    }

    private void AskLead()
    {
        var lines = Ticked.Select(group => $"- {(group.Item.Ask.Length > 0 ? group.Item.Ask : group.Text)} ({group.Detail})");

        _actions.AskLead!($"The owner asks you about these waits in {_actions.Project}. For each, settle it and record it with "
                          + "record_decision and set_waiting, or tell the owner what you need from him:"
                          + Environment.NewLine + string.Join(Environment.NewLine, lines));
        Finished?.Invoke(this, EventArgs.Empty);
    }

    private void GoToObjective()
    {
        var objective = TickedObjectives.Single();

        Finished?.Invoke(this, EventArgs.Empty);
        _actions.GoToObjective(objective);
    }

    private string Asked() => string.Join("; ", Ticked.Select(group =>
        $"{group.Item.Ask} (objective{(group.TickedObjectives.Count() == 1 ? string.Empty : "s")} {string.Join(", ", group.TickedObjectives)})"));

    private string Count() => Ticked.Count == 1 ? "one wait" : $"{Ticked.Count} waits";

    private void TickAll(bool ticked)
    {
        foreach (var group in Groups)
        {
            group.IsTicked = ticked;
        }
    }

    private void Recount()
    {
        foreach (var name in new[]
                 {
                     nameof(CountText), nameof(AnswerToolTip), nameof(DoneToolTip), nameof(ReassignToolTip), nameof(AskLeadToolTip),
                     nameof(GoToObjectiveToolTip),
                 })
        {
            Raise(name);
        }

        CommandManager.InvalidateRequerySuggested();
    }
}
