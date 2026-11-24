using Theseus.Core.Breakers;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Projects;
using Theseus.Core.Running;

namespace Theseus.App.ViewModels;

/// <summary>Headless runs on the board: card faces, the running and queued counts, breakers and their Reset, and Open in a tab.</summary>
public sealed partial class ShellViewModel : IRunRequests
{
    /// <summary>Who a breaker's pause is recorded as, so the board can tell it from the owner's own pause.</summary>
    public const string BreakerPauser = "breaker";

    private string _headlessCounts = string.Empty;
    private RelayCommand? _resetBreakerCommand;
    private RelayCommand? _trippedBreakersCommand;
    private RelayCommand? _runInTabCommand;
    private RelayCommand? _runHeadlessCommand;
    private RelayCommand? _runByDefaultCommand;
    private RelayCommand? _openInTabCommand;
    private RelayCommand? _showOutputCommand;

    /// <summary>The runner that runs headless cards, or null when every card runs in a tab.</summary>
    public HeadlessRunner? Headless { get; set; }

    /// <summary>Opens the window that follows a run's output: its title and log file.</summary>
    public Action<string, string>? ShowRunOutput { get; set; }

    /// <summary>The status bar's count of headless runs, such as "Headless: 2 running, 1 queued"; empty when there are none.</summary>
    public string HeadlessCounts
    {
        get => _headlessCounts;
        private set => Set(ref _headlessCounts, value);
    }

    /// <summary>Resets a card's breaker (card menu).</summary>
    public RelayCommand ResetBreakerCommand => _resetBreakerCommand ??= new RelayCommand(
        parameter => { if (parameter is WorkstreamCardViewModel card) { ResetBreakers([card.Key]); } },
        parameter => parameter is WorkstreamCardViewModel { IsBreakerTripped: true });

    /// <summary>Tools › Tripped breakers…: every tripped card, ticked, with one Reset command.</summary>
    public RelayCommand TrippedBreakersCommand => _trippedBreakersCommand ??= new RelayCommand(_ => TrippedBreakers());

    /// <summary>Runs a card in a tab from now on.</summary>
    public RelayCommand RunInTabCommand => _runInTabCommand ??= new RelayCommand(parameter => SetRunMode(parameter, RunMode.Tab));

    /// <summary>Runs a card headless from now on.</summary>
    public RelayCommand RunHeadlessCommand => _runHeadlessCommand ??= new RelayCommand(parameter => SetRunMode(parameter, RunMode.Headless));

    /// <summary>Gives a card its project's or its role's mode again.</summary>
    public RelayCommand RunByDefaultCommand => _runByDefaultCommand ??= new RelayCommand(parameter => SetRunMode(parameter, null));

    /// <summary>Ends a card's headless run and resumes its conversation in a tab.</summary>
    public RelayCommand OpenInTabCommand => _openInTabCommand ??= new RelayCommand(
        parameter => { if (parameter is WorkstreamCardViewModel card) { _ = OpenInTab(card); } },
        parameter => parameter is WorkstreamCardViewModel card && Headless?.StateOf(card.Key) is not null);

    /// <summary>Shows a card's headless run's output.</summary>
    public RelayCommand ShowOutputCommand => _showOutputCommand ??= new RelayCommand(
        parameter =>
        {
            if (parameter is WorkstreamCardViewModel card && Headless?.StateOf(card.Key)?.LogPath is { } log)
            {
                ShowRunOutput?.Invoke(card.Title, log);
            }
        },
        parameter => parameter is WorkstreamCardViewModel card && Headless?.StateOf(card.Key)?.LogPath is not null);

    /// <summary>Brings every card's headless line and the status bar's counts up to date.</summary>
    public void SyncHeadless()
    {
        if (Headless is not { } runner)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (var card in Board.Cards)
        {
            var state = runner.StateOf(card.Key);
            card.HeadlessLine = HeadlessFace.For(state, now, TimeZoneInfo.Local) ?? string.Empty;
            card.HeadlessDetail = state is { IsRunning: true, LastLine: { } line } ? line : string.Empty;
        }

        HeadlessCounts = runner.Running + runner.Queued == 0
            ? string.Empty
            : $"Headless: {runner.Running} running, {runner.Queued} queued";
    }

    /// <summary>A card's breaker tripped: pause it, record why on it and in its History, and say so.</summary>
    public void BreakerTripped(string key, BreakerTrip trip)
    {
        ArgumentNullException.ThrowIfNull(trip);

        if (Board.Card(key) is not { } card)
        {
            return;
        }

        card.Workstream = card.Workstream with { Breaker = trip, IsPaused = true, PausedBy = BreakerPauser, PauseReason = trip.Detail };
        Board.Persist(card);
        _cardEvents.Append(new CardEvent(DateTimeOffset.UtcNow, key, trip.Detail));
        TheseusLog.Warning("{Card}: {Detail}", key, trip.Detail);
        Notice = $"{card.Title}: {trip.Detail} Reset it from the card when fixed.";
        Refresh();
    }

    /// <summary>Clears the breakers of the cards named: unpauses each one its breaker paused, and starts its count again from now.</summary>
    public void ResetBreakers(IEnumerable<string> keys)
    {
        ArgumentNullException.ThrowIfNull(keys);

        var reset = new List<string>();

        foreach (var key in keys)
        {
            if (Board.Card(key) is not { Workstream.Breaker: not null } card)
            {
                continue;
            }

            var byBreaker = string.Equals(card.Workstream.PausedBy, BreakerPauser, StringComparison.Ordinal);
            card.Workstream = card.Workstream with
            {
                Breaker = null,
                BreakerResetUtc = DateTimeOffset.UtcNow,
                IsPaused = !byBreaker && card.Workstream.IsPaused,
                PausedBy = byBreaker ? null : card.Workstream.PausedBy,
                PauseReason = byBreaker ? null : card.Workstream.PauseReason,
            };
            Board.Persist(card);
            _cardEvents.Append(new CardEvent(DateTimeOffset.UtcNow, key, "Breaker reset by the owner"));
            reset.Add(card.Title);
        }

        if (reset.Count > 0)
        {
            TheseusLog.Information("Reset the breakers of {Cards}", string.Join(", ", reset));
            Notice = $"Reset the breaker of {string.Join(", ", reset)}. It starts again when you start it.";
            Refresh();
        }
    }

    private void TrippedBreakers()
    {
        var tripped = Board.Cards.Where(card => card.Workstream.Breaker is not null).ToList();

        if (tripped.Count == 0)
        {
            Notice = "No card's breaker has tripped.";

            return;
        }

        var list = new Checklist("Tripped breakers",
            "Each card ticked has its breaker reset: it is unpaused if its breaker paused it, and its count of failed runs starts "
            + "again. Nothing starts it; start it yourself when the cause is fixed.",
            "Reset",
            [.. tripped.Select(card => new ChecklistRow(card.Key, card.Title,
                $"{card.Workstream.Breaker!.Kind} · tripped {card.Workstream.Breaker.TrippedUtc.ToLocalTime():ddd HH:mm} · card {card.Key}",
                card.Workstream.Breaker.Detail))],
            ticked => ticked.Count == 0 ? "Nothing ticked" : $"Reset {ticked.Count}");

        var answer = AskChecklist is { } ask ? ask(new ChecklistViewModel(list)) : null;

        if (answer is { Count: > 0 })
        {
            ResetBreakers(answer);
        }
    }

    private void SetRunMode(object? parameter, RunMode? mode)
    {
        if (parameter is not WorkstreamCardViewModel card)
        {
            return;
        }

        card.Workstream = card.Workstream with { RunMode = mode };
        Board.Persist(card);
        Notice = $"{card.Title} runs {(mode is { } chosen ? chosen == RunMode.Tab ? "in a tab" : "headless" : "as its project or role says")} from its next start.";
    }

    private async Task OpenInTab(WorkstreamCardViewModel card)
    {
        if (Headless is not { } runner)
        {
            return;
        }

        Notice = $"Opening {card.Title} in a tab: ending its headless run first.";
        await runner.InterruptAsync(card.Key).ConfigureAwait(true);

        card.Workstream = card.Workstream with { RunMode = RunMode.Tab };
        Board.Persist(card);
        TheseusLog.Information("Opening {Card} in a tab, resuming session {Session}", card.Key, card.Workstream.LastSessionId);
        StartMany([card.Workstream], StartMode.Resume);
    }

    /// <summary>
    /// Starts, queues or refuses a run that a duty, the overnight queue or Theseus itself asked for: in the card's mode,
    /// inside its budget, under the cap (interface agreed with the duties workstream, 2026-11-24). Call on the UI thread.
    /// </summary>
    public RunRequestOutcome Request(RunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (Board.Card(request.Workstream) is not { } card)
        {
            return new RunRefused(RefusalReason.CannotStart, $"There is no card {request.Workstream}.");
        }

        var workstream = card.Workstream;

        if (workstream.Breaker is { } trip)
        {
            return new RunRefused(RefusalReason.BreakerTripped, trip.Detail);
        }

        if (workstream.IsPaused)
        {
            return new RunRefused(RefusalReason.Paused, $"{workstream.Title} is paused{(workstream.PauseReason is { } why ? $": {why}" : ".")}");
        }

        if (Headless?.Holds(workstream.Key) == true || card.Lifecycle is SessionLifecycle.Launching or SessionLifecycle.Running or SessionLifecycle.Idle or SessionLifecycle.NeedsAttention)
        {
            return new RunRefused(RefusalReason.AlreadyRunning, $"{workstream.Title} already has a session going or queued.");
        }

        if (!string.IsNullOrWhiteSpace(request.Prompt))
        {
            workstream = workstream with { StartInstruction = request.Prompt };
        }

        workstream = AimedWriter(Board.Pin(workstream));
        TheseusLog.Information("{By} asked for a run of {Workstream} ({Reason})", request.RequestedBy, workstream.Key, request.Reason);

        if (_launcher.Launch(workstream, _plans.PathFor(workstream.Key), StartMode.Resume, request.Reason) is not { } sessionId)
        {
            var refusal = _launcher.LastRefusal ?? $"{workstream.Title} could not be started.";

            return new RunRefused(refusal.Contains("Stopped at its limit", StringComparison.Ordinal) ? RefusalReason.BudgetReached : RefusalReason.CannotStart, refusal);
        }

        Remember(workstream, sessionId);
        _notYetRegistered[sessionId] = DateTimeOffset.UtcNow;
        RunCommands.Started(workstream.Key);
        SyncHeadless();

        var mode = RunModeChoice.For(workstream, _settings.Read().ProjectRunModes);

        return Headless?.StateOf(workstream.Key)?.QueuePosition is { } position
            ? new RunQueued(position)
            : new RunStarted(sessionId, mode);
    }
}
