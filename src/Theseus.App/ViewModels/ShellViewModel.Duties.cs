using System.Globalization;
using System.Windows.Threading;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Duties;
using Theseus.Core.Projects;
using Theseus.Core.Settings;
using Theseus.Core.Terminal;

namespace Theseus.App.ViewModels;

/// <summary>
/// Duties and the overnight queue on the board: a timer and every board refresh look at each duty, give the card its run
/// in the way its state allows, and keep every run, wait, skip and end in the card's History and the Theseus log.
/// </summary>
public sealed partial class ShellViewModel : IDutyDelivery
{
    private static readonly TimeSpan DutyLook = TimeSpan.FromSeconds(30);
    private DutyBook? _duties;
    private DispatcherTimer? _dutyTimer;
    private bool _dutyTicking;
    private IReadOnlyList<QueueCandidate> _queueCandidates = [];

    /// <summary>The board's duties, set once at start-up; the timer starts with it.</summary>
    public DutyBook? Duties
    {
        get => _duties;
        set
        {
            _duties = value;

            if (value is null || _dutyTimer is not null)
            {
                return;
            }

            _dutyTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = DutyLook };
            _dutyTimer.Tick += (_, _) => TickDuties();
            _dutyTimer.Start();
            value.Changed += (_, _) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(ShowDuties);
        }
    }

    /// <summary>Shows the duties checklist; set by the app.</summary>
    public Action<DutiesViewModel>? ShowDutiesWindow { get; set; }

    /// <summary>Opens the duty dialog, true once OK saved it; set by the app.</summary>
    public Func<DutyEditViewModel, bool>? AskDuty { get; set; }

    /// <summary>Asks before deleting duties, true to delete; set by the app.</summary>
    public Func<string, bool>? ConfirmDutyDelete { get; set; }

    /// <summary>Tools › Duties…: every duty on the board.</summary>
    public RelayCommand OpenDutiesCommand => _openDuties ??= new RelayCommand(_ => OpenDuties("the board", _ => true, null),
        _ => _duties is not null);

    private RelayCommand? _openDuties;

    /// <summary>Opens the duties checklist over the duties <paramref name="shows"/> picks.</summary>
    private void OpenDuties(string scope, Func<Duty, bool> shows, string? card)
    {
        if (_duties is not { } book || ShowDutiesWindow is null)
        {
            return;
        }

        IReadOnlyList<(string Key, string Title)> cards = [.. Board.Cards
            .Where(each => !each.Workstream.IsArchived && (card is null || string.Equals(each.Key, card, StringComparison.OrdinalIgnoreCase)))
            .Select(each => (each.Key, each.Title))
            .OrderBy(each => each.Title, StringComparer.CurrentCultureIgnoreCase)];
        var actions = new DutiesActions(book, TitleOf, RecordDutyEvent, model => AskDuty?.Invoke(model) == true,
            text => ConfirmDutyDelete?.Invoke(text) == true, cards, TickDuties,
            (key, name) => [.. _cardEvents.For(key)
                .Where(each => each.Text.StartsWith($"Duty \"{name}\"", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(each => each.AtUtc)
                .Take(20)
                .Select(each => $"{each.AtUtc.ToLocalTime().ToString("ddd HH:mm", CultureInfo.InvariantCulture)}  {each.Text}")]);

        ShowDutiesWindow(new DutiesViewModel(actions, scope, shows, card));
    }

    /// <summary>Keeps a change to a card's duties in its History and in the log, as an MCP call or a window makes it.</summary>
    public void RecordDutyEvent(string card, string text)
    {
        _cardEvents.Append(new CardEvent(DateTimeOffset.UtcNow, card, text));
        TheseusLog.Information("Duty on {Card}: {What}", card, text);
    }

    /// <summary>
    /// Looks at every duty and the queue once: follows each run in flight to its outcome, then runs, waits, skips or ends
    /// each due duty, then resumes the cards the queue chooses.
    /// </summary>
    public void TickDuties()
    {
        if (_duties is not { } book || _dutyTicking)
        {
            return;
        }

        _dutyTicking = true;

        try
        {
            var board = _settings.Read();
            var settings = board.Duties;
            var cards = Board.Cards.ToDictionary(card => card.Key, card => DutyCardOf(card, board), StringComparer.OrdinalIgnoreCase);

            FollowRuns(book, cards, settings.FailureLimit);

            foreach (var decision in DutyScheduler.Decide(book.Live, cards, settings, DateTimeOffset.UtcNow, TimeZoneInfo.Local, CriterionTicked))
            {
                Act(book, decision);
            }

            ResumeFromQueue(book, settings);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            TheseusLog.Error(exception, "Looking at the duties failed; they are looked at again in {Seconds}s", DutyLook.TotalSeconds);
        }
        finally
        {
            _dutyTicking = false;
            ShowDuties();
        }
    }

    /// <inheritdoc />
    public DutyDelivery Deliver(string card, DutyRunReason reason, string instruction)
    {
        if (Board.Card(card) is not { } target)
        {
            return DutyDelivery.Failed("its card is not on the board");
        }

        if (target.IsLive)
        {
            return HandInFromTheseus(target, instruction) is { } refusal
                ? DutyDelivery.Failed(refusal)
                : DutyDelivery.Delivered("handed in");
        }

        var trigger = reason == DutyRunReason.Queue ? "Queue" : "Duty";

        return StartForSession([target.Workstream with { StartInstruction = instruction }], trigger) is { } notStarted
            ? DutyDelivery.Failed(notStarted)
            : DutyDelivery.Delivered("started");
    }

    private void FollowRuns(DutyBook book, IReadOnlyDictionary<string, DutyCard> cards, int failureLimit)
    {
        foreach (var duty in book.Live.Where(duty => duty.State.DeliveredUtc is not null))
        {
            var (outcome, detail, seen) = DutyScheduler.Observe(duty, cards.GetValueOrDefault(duty.Card), DateTimeOffset.UtcNow);

            if (outcome is null)
            {
                if (seen)
                {
                    book.SeenWorking(duty.Id);
                }

                continue;
            }

            var (_, paused) = book.Outcome(duty.Id, outcome.Value, detail, failureLimit);
            Record(duty, $"run {duty.State.RunCount} {DutyPrompt.Word(outcome)}{(detail is null ? string.Empty : $": {detail}")}");

            if (paused)
            {
                Record(duty, $"paused by Theseus after {failureLimit} failed runs");
                Warn($"The duty \"{duty.Name}\" on {TitleOf(duty.Card)} paused itself after {failureLimit} failed runs: {detail}");
            }
        }
    }

    private void Act(DutyBook book, DutyDecision decision)
    {
        var duty = decision.Duty;

        switch (decision.Action)
        {
            case DutyAction.Run:
                Run(book, duty, decision.Missed);
                break;
            case DutyAction.Skip:
                book.Skipped(duty.Id, decision.Reason);
                Record(duty, $"skipped: {decision.Reason}");
                break;
            case DutyAction.Wait:
                if (!string.Equals(duty.State.Waiting, decision.Reason, StringComparison.Ordinal))
                {
                    book.Waiting(duty.Id, decision.Reason);
                    TheseusLog.Information("Duty {Id} \"{Name}\" on {Card}: waiting, {Why}", duty.Id, duty.Name, duty.Card, decision.Reason);
                }

                break;
            case DutyAction.End:
                book.End(duty.Id, decision.Reason);
                Record(duty, $"ended: {decision.Reason}");
                break;
        }
    }

    private void Run(DutyBook book, Duty duty, int missed)
    {
        var since = duty.State.LastRunUtc ?? duty.SetUtc;
        IReadOnlyList<string> changes = [.. _cardEvents.For(duty.Card)
            .Where(each => each.AtUtc > since && !each.Text.StartsWith("Duty ", StringComparison.Ordinal))
            .Select(each => $"{each.AtUtc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture)} {each.Text}")];
        var text = DutyPrompt.For(duty, changes, missed, TimeZoneInfo.Local);
        var delivery = Deliver(duty.Card, DutyRunReason.Duty, text);

        book.Ran(duty.Id);

        if (delivery.Kind == DutyDeliveryKind.Delivered)
        {
            Record(duty, $"run {duty.State.RunCount + 1} given: {delivery.Detail}{(missed > 0 ? $", catching up {missed} missed" : string.Empty)}");

            return;
        }

        var (_, paused) = book.Outcome(duty.Id, DutyOutcome.NotDelivered, delivery.Detail, _settings.Read().Duties.FailureLimit);
        Record(duty, $"run {duty.State.RunCount + 1} not delivered: {delivery.Detail}");

        if (paused)
        {
            Warn($"The duty \"{duty.Name}\" on {TitleOf(duty.Card)} paused itself: {delivery.Detail}");
        }
    }

    private void ResumeFromQueue(DutyBook book, Core.Settings.DutySettings settings)
    {
        if (settings.QueueProjects.Count == 0)
        {
            return;
        }

        foreach (var resume in OvernightQueue.Choose(_queueCandidates, book.QueueRecordFor, settings, DateTimeOffset.UtcNow, TimeZoneInfo.Local))
        {
            var delivery = Deliver(resume.Card, DutyRunReason.Queue, resume.Instruction);
            var said = delivery.Kind == DutyDeliveryKind.Delivered
                ? $"Resumed by the overnight queue at {resume.Status}"
                : $"The overnight queue could not resume it: {delivery.Detail}";

            // Recorded either way, so a card it cannot reach is not tried again every half minute.
            book.QueueResumed(resume.Card, resume.Status);
            RecordDutyEvent(resume.Card, said);
        }
    }

    /// <summary>The queue's view of each current piece of a project, gathered as the projects are read.</summary>
    private void GatherQueueCandidates(string project, IReadOnlyList<PieceOfWork> pieces, TheseusSettings settings, List<QueueCandidate> into)
    {
        if (!settings.Duties.QueueIsOn(project))
        {
            return;
        }

        var projectRunning = !settings.ProjectRunStates.ContainsKey(project);

        foreach (var piece in pieces.Where(piece => piece.IsCurrentPiece && piece.Card is not null))
        {
            if (Board.Card(piece.CardKey) is not { } card)
            {
                continue;
            }

            var status = piece.Status;

            into.Add(new QueueCandidate(DutyCardOf(card, settings), project)
            {
                ProjectRunning = projectRunning,
                HasUnfinishedPiece = status.State is StageNodeState.Busy or StageNodeState.ToDo or StageNodeState.DoneNoRecord,
                WaitsForOwner = status.WaitsForYou || status.State == StageNodeState.WaitingOnYou,
                IsVerifier = card.Workstream.Role == WorkstreamRole.Verifier,
                StatusSinceUtc = card.LifecycleSinceUtc,
                PieceTitle = string.IsNullOrWhiteSpace(piece.Workstream) ? piece.Name : piece.Workstream,
                Status = status.Text,
            });
        }
    }

    private DutyCard DutyCardOf(WorkstreamCardViewModel card, TheseusSettings settings)
    {
        var workstream = card.Workstream;
        var step = card.IsLive ? StepsFor([card]).FirstOrDefault() : null;

        return new DutyCard(card.Key, card.Title, card.Lifecycle, card.IsLive || card.Lifecycle == SessionLifecycle.Ghost)
        {
            PausedByOwner = workstream is { IsPaused: true, PausedBy: null },
            PausedByCard = workstream is { IsPaused: true, PausedBy: { } by } ? TitleOf(by) : null,
            IsBlocked = workstream.IsBlocked,
            IsArchived = workstream.IsArchived,
            StoppedByOwner = settings.CardsStoppedByOwner.Contains(card.Key, StringComparer.OrdinalIgnoreCase)
                             || (workstream.Project is { } project && settings.ProjectRunStates.ContainsKey(project)),
            IsAsking = card.Question is not null,
            IsStopping = step is not null && _stopRequests.Waiting(step.SessionId) is not null,
        };
    }

    private bool CriterionTicked(Duty duty)
    {
        if (Board.Card(duty.Card)?.Workstream.Project is not { } project || duty.End.Criterion is not { } criterion)
        {
            return false;
        }

        return _projectReads.Current.FirstOrDefault(reading => string.Equals(reading.Name, project, StringComparison.OrdinalIgnoreCase))
            ?.File?.Objectives.SelectMany(objective => objective.Criteria)
            .Any(each => each.IsTicked && string.Equals(each.Text.Trim(), criterion.Trim(), StringComparison.OrdinalIgnoreCase)) == true;
    }

    /// <summary>Puts each card's duties on its face.</summary>
    private void ShowDuties()
    {
        if (_duties is not { } book)
        {
            return;
        }

        var live = book.Live;

        foreach (var card in Board.Cards)
        {
            var mine = live.Where(duty => string.Equals(duty.Card, card.Key, StringComparison.OrdinalIgnoreCase)).ToList();
            card.DutyText = string.Join(Environment.NewLine, DutyFace.Lines(mine, TimeZoneInfo.Local));
            card.DutyToolTip = DutyFace.ToolTip(mine, key => TitleOf(key));
            var key = card.Key;
            var title = card.Title;
            card.DutiesCommand ??= new RelayCommand(_ =>
                OpenDuties(title, duty => string.Equals(duty.Card, key, StringComparison.OrdinalIgnoreCase), key));
        }

        MirrorDutiesToProjects(live);

        foreach (var project in Board.ProjectCards)
        {
            var name = project.Name;
            project.DutiesCommand ??= new RelayCommand(_ => OpenDuties(name, duty =>
                string.Equals(Board.Card(duty.Card)?.Workstream.Project, name, StringComparison.OrdinalIgnoreCase), null));
        }
    }

    /// <summary>
    /// Rewrites each project file's duty lines when they differ from its duties: under the objective whose table names each
    /// duty's card. A file that cannot be written now is left, and tried again at the next look.
    /// </summary>
    private void MirrorDutiesToProjects(IReadOnlyList<Duty> live)
    {
        foreach (var reading in _projectReads.Current.Where(reading => reading.IsReadable && reading.File is not null && reading.Text is not null))
        {
            var byObjective = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

            foreach (var objective in reading.File!.Objectives)
            {
                var cards = objective.Workstreams.Select(row => row.Workstream).ToHashSet(StringComparer.OrdinalIgnoreCase);
                IReadOnlyList<string> lines = [.. live.Where(duty => cards.Contains(duty.Card))
                    .OrderBy(duty => TitleOf(duty.Card)).ThenBy(duty => duty.Name)
                    .Select(duty => ProjectDutyLines.Line(duty, TitleOf(duty.Card)))];

                if (lines.Count > 0)
                {
                    byObjective[objective.Number] = lines;
                }
            }

            var text = reading.Text!;

            if (byObjective.Count == 0 && !text.Contains(ProjectDutyLines.Prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (string.Equals(ProjectDutyLines.Write(text, byObjective), text, StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                WriteProjectFile(reading.Name, current => ProjectDutyLines.Write(current, byObjective));
                TheseusLog.Information("Wrote the duty lines of {Project}", reading.Name);
            }
            catch (ProjectEditRefusedException refused)
            {
                TheseusLog.Information("The duty lines of {Project} were not written now: {Why}", reading.Name, refused.Message);
            }
        }
    }

    private void Record(Duty duty, string what)
    {
        _cardEvents.Append(new CardEvent(DateTimeOffset.UtcNow, duty.Card, $"Duty \"{duty.Name}\" {what}"));
        TheseusLog.Information("Duty {Id} \"{Name}\" on {Card}: {What}", duty.Id, duty.Name, duty.Card, what);
    }

    private string TitleOf(string key) => Board.Card(key)?.Title ?? key;

    /// <summary>
    /// Gives an open session Theseus's own instruction as its next turn, in its own words rather than as work handed in by a
    /// card: the tell waits for the hook, and the session is woken to take it.
    /// </summary>
    private string? HandInFromTheseus(WorkstreamCardViewModel card, string instruction)
    {
        if (!Theseus.Core.Stopping.HookInstallation.IsInstalled(SettingsFile()))
        {
            return "Theseus's stop hook is not installed, so it cannot hand work to an open session";
        }

        if (StepsFor([card]).FirstOrDefault() is not { } step)
        {
            return "it has no session to hand it to";
        }

        if (!Tells.Queue(card.Key, card.Title, step.SessionId, instruction, DateTimeOffset.UtcNow))
        {
            return "its instruction could not be left for it";
        }

        return Wake(step, HandInMarker) == ConsoleTypingOutcome.Sent
            ? null
            : "its session could not be woken to take it";
    }
}
