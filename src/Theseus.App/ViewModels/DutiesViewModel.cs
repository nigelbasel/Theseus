using System.Collections.ObjectModel;
using System.Windows.Input;
using Theseus.Core.Duties;

namespace Theseus.App.ViewModels;

/// <summary>
/// The duties checklist: every duty of the board, a project or a card, ticked or not, and a command bar acting on what is
/// ticked: Run now, Pause, Resume, Edit…, Delete…, and Add… for a new one (Nigel's rule for lists of items, 2026-11-24).
/// </summary>
public sealed class DutiesViewModel : ObservableObject
{
    private readonly DutiesActions _actions;
    private readonly Func<Duty, bool> _shows;
    private readonly string? _card;

    /// <summary>Creates the checklist.</summary>
    /// <param name="actions">What the bar reaches for.</param>
    /// <param name="scope">What it lists, for its title: "the board", a project's name or a card's title.</param>
    /// <param name="shows">Which duties it lists.</param>
    /// <param name="card">The card a new duty goes on when the list is one card's; null to choose.</param>
    public DutiesViewModel(DutiesActions actions, string scope, Func<Duty, bool> shows, string? card = null)
    {
        ArgumentNullException.ThrowIfNull(actions);

        _actions = actions;
        _shows = shows;
        _card = card;
        Title = $"Duties: {scope}";
        RunNowCommand = new RelayCommand(_ => RunNow(), _ => Ticked.Count > 0);
        PauseCommand = new RelayCommand(_ => Pause(), _ => Ticked.Any(row => !row.Duty.IsPaused));
        ResumeCommand = new RelayCommand(_ => Resume(), _ => Ticked.Any(row => row.Duty.IsPaused));
        EditCommand = new RelayCommand(_ => Edit(Ticked.Single().Duty), _ => Ticked.Count == 1);
        DeleteCommand = new RelayCommand(_ => Delete(), _ => Ticked.Count > 0);
        AddCommand = new RelayCommand(_ => Edit(null), _ => _card is not null || actions.Cards.Count > 0);
        SelectAllCommand = new RelayCommand(_ => TickAll(true), _ => Rows.Count > 0);
        ClearCommand = new RelayCommand(_ => TickAll(false), _ => Ticked.Count > 0);
        Reload([]);
    }

    /// <summary>The dialog's title.</summary>
    public string Title { get; }

    /// <summary>What the dialog is for.</summary>
    public string Intro =>
        "Each duty gives its card an instruction on a cadence, as its state allows: handed in when it is idle, started when it is "
        + "stopped, held while it works or asks you something, skipped while it is paused. Tick duties, then choose an action.";

    /// <summary>Every duty it lists.</summary>
    public ObservableCollection<DutyRowViewModel> Rows { get; } = [];

    /// <summary>"1 of 3 selected", or that there are none.</summary>
    public string CountText => Rows.Count == 0 ? "No duties" : $"{Ticked.Count} of {Rows.Count} selected";

    /// <summary>Why Edit… is disabled, or what it does.</summary>
    public string EditToolTip => Ticked.Count == 1 ? "Change the ticked duty" : "Tick exactly one duty to edit it";

    /// <summary>Runs every ticked duty now, as its card's state allows, without moving its cadence.</summary>
    public ICommand RunNowCommand { get; }

    /// <summary>Pauses every ticked duty.</summary>
    public ICommand PauseCommand { get; }

    /// <summary>Resumes every ticked duty.</summary>
    public ICommand ResumeCommand { get; }

    /// <summary>Opens the ticked duty.</summary>
    public ICommand EditCommand { get; }

    /// <summary>Deletes every ticked duty, after asking.</summary>
    public ICommand DeleteCommand { get; }

    /// <summary>Opens a new duty.</summary>
    public ICommand AddCommand { get; }

    /// <summary>Ticks every duty.</summary>
    public ICommand SelectAllCommand { get; }

    /// <summary>Unticks every duty.</summary>
    public ICommand ClearCommand { get; }

    private List<DutyRowViewModel> Ticked => [.. Rows.Where(row => row.IsTicked)];

    /// <summary>Reads the duties again, keeping what was ticked.</summary>
    public void Reload() => Reload([.. Ticked.Select(row => row.Duty.Id)]);

    private void Reload(IReadOnlyCollection<string> ticked)
    {
        Rows.Clear();

        foreach (var duty in _actions.Book.Live.Where(_shows).OrderBy(duty => _actions.TitleOf(duty.Card)).ThenBy(duty => duty.Name))
        {
            Rows.Add(new DutyRowViewModel(duty, _actions.TitleOf(duty.Card), duty.SetBy is { } by ? _actions.TitleOf(by) : "you",
                ticked.Contains(duty.Id), Recount));
        }

        Recount();
    }

    private void RunNow()
    {
        foreach (var row in Ticked)
        {
            _actions.Book.RunNow(row.Duty.Id);
            _actions.Record(row.Duty.Card, $"Duty \"{row.Duty.Name}\" run now by you");
        }

        _actions.RunNow();
        Reload();
    }

    private void Pause()
    {
        foreach (var row in Ticked.Where(row => !row.Duty.IsPaused))
        {
            _actions.Book.Pause(row.Duty.Id, Duty.Owner, null);
            _actions.Record(row.Duty.Card, $"Duty \"{row.Duty.Name}\" paused by you");
        }

        Reload();
    }

    private void Resume()
    {
        foreach (var row in Ticked.Where(row => row.Duty.IsPaused))
        {
            _actions.Book.Resume(row.Duty.Id);
            _actions.Record(row.Duty.Card, $"Duty \"{row.Duty.Name}\" resumed by you");
        }

        Reload();
    }

    private void Delete()
    {
        var ticked = Ticked;
        var names = string.Join(", ", ticked.Select(row => $"\"{row.Duty.Name}\" on {_actions.TitleOf(row.Duty.Card)}"));

        if (!_actions.ConfirmDelete($"Delete {names}? It stops running and its run history goes."))
        {
            return;
        }

        foreach (var row in ticked)
        {
            _actions.Book.Delete(row.Duty.Id);
            _actions.Record(row.Duty.Card, $"Duty \"{row.Duty.Name}\" deleted by you");
        }

        Reload([]);
    }

    private void Edit(Duty? duty)
    {
        var model = new DutyEditViewModel(duty, _card ?? duty?.Card, _actions.Cards, _actions.Book,
            duty is null ? [] : _actions.History(duty.Card, duty.Name));

        if (!_actions.Edit(model) || model.Saved is not { } saved)
        {
            return;
        }

        _actions.Record(saved.Card, $"Duty \"{saved.Name}\" {(duty is null ? "set" : "changed")} by you: {saved.Cadence.Describe()}");
        Reload();
    }

    private void TickAll(bool ticked)
    {
        foreach (var row in Rows)
        {
            row.IsTicked = ticked;
        }
    }

    private void Recount()
    {
        Raise(nameof(CountText));
        Raise(nameof(EditToolTip));
        CommandManager.InvalidateRequerySuggested();
    }
}
