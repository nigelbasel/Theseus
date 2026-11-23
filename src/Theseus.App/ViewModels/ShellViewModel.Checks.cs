using System.Windows.Threading;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// The independent check run without the owner's press (git-22, 2026-11-23: nothing Theseus makes waits on him): each
/// project's waiting builds get their verifier, or their scenario writer, as <see cref="CheckSteps"/> decides, and the
/// Waits line says what is under way. Clear up verifiers… stays a press: archiving is his.
/// </summary>
public sealed partial class ShellViewModel
{
    // The dispatcher the view model was made on, the window's: the steps are queued there, after the board is drawn.
    private readonly Dispatcher _checksDispatcher = Dispatcher.CurrentDispatcher;

    // Each step is tried once for a build's session, or a project's set of unnamed builds, so one that cannot be taken is not
    // tried on every refresh, while a build reworked in a new session is checked again.
    private readonly HashSet<string> _checksTried = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _checksQueued = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CheckPlan> _checkPlans = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether Theseus makes and starts the checks itself. Set by the app alone: a view model built by a test or a tool that
    /// reads the window never starts a session.
    /// </summary>
    public bool RunsChecksItself { get; set; }

    /// <summary>Shows a checklist confirmation, returning the keys ticked, or null when cancelled. Set by the app.</summary>
    public Func<ChecklistViewModel, IReadOnlyList<string>?>? AskChecklist { get; set; }

    /// <summary>Each project's steps as last worked out, by project, whether or not they were taken: read by tools.</summary>
    public IReadOnlyDictionary<string, CheckPlan> CheckPlans => _checkPlans;

    /// <summary>The steps for a project's pieces, from the holdout folders and the board as last refreshed.</summary>
    private CheckPlan CheckPlanOf(ProjectFileReading reading, IReadOnlyList<PieceOfWork> pieces)
    {
        var writers = WritersOf(reading.Name);
        var location = _holdouts.LocationOfProject(reading.Name);
        var plan = CheckSteps.For(pieces, piece => Board.Card(piece.CardKey) is { } card && _holdouts.IsNamed(card.Workstream),
            writerStands: writers.Count > 0,
            writerRunning: writers.Any(card => card.IsLive || _launcher.InFlight.IsInFlight(card.Key)),
            canWrite: !string.IsNullOrWhiteSpace(location.Folder) && !location.IsMissing,
            idleWriter: writers.FirstOrDefault(card => !card.IsLive && !_launcher.InFlight.IsInFlight(card.Key) && !IsHeld(card))?.Key,
            isChecked: piece => IsChecked(piece.CardKey),
            isHeld: piece => Board.Card(piece.CardKey) is not { } card || IsHeld(card));
        _checkPlans[reading.Name] = plan;

        return plan;
    }

    private List<WorkstreamCardViewModel> WritersOf(string project) =>
    [
        .. Board.Cards.Where(card => card.Workstream is { Role: WorkstreamRole.ScenarioWriter, IsArchived: false }
                                     && string.Equals(WriterProject(card.Workstream), project, StringComparison.OrdinalIgnoreCase)),
    ];

    // A build whose verifier reported has had its check: it waits for its own records, and is not checked again however
    // often Theseus restarts (review of the follow-on, M1). A rejected report does not count.
    private bool IsChecked(string build) =>
        (Chains?.AllLinks ?? []).Any(link => link.IsActive && link.Kind == LinkKind.ReportsBack
                                             && string.Equals(link.To, build, StringComparison.OrdinalIgnoreCase)
                                             && link.LatestReport is not null && !link.ReportIsRejected
                                             && Board.Card(link.From)?.Workstream.Role == WorkstreamRole.Verifier);

    // What the owner held is not started by Theseus: paused, blocked, archived or stopped by him (review of the follow-on, M3).
    private bool IsHeld(WorkstreamCardViewModel card) =>
        card.Workstream is { IsArchived: true } or { IsBlocked: true } or { IsPaused: true }
        || card.Workstream.PausedBy is not null
        || _settings.Read().CardsStoppedByOwner.Contains(card.Key, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Whether a verifier cannot run under today's rules: its brief predates Checks lines, or no scenario names its build.
    /// It neither stands nor asks for a start; Clear up verifiers… takes it (git-22, 2026-11-23).
    /// </summary>
    // A running one is never obsolete: it still stands, so no second verifier is made beside it (review of the follow-ups, S1).
    private bool IsObsolete(WorkstreamCardViewModel card) =>
        card.Workstream.Role == WorkstreamRole.Verifier && !card.IsLive
        && (!card.Workstream.Prompt.Contains(VerifierBrief.ChecksMarker, StringComparison.Ordinal)
            || (SubjectOf(card.Key) is { } subject && Board.Card(subject) is { } build && _holdouts.Names(build.Workstream) == false));

    // The card a verifier checks: its latest active report-back link's other end.
    private string? SubjectOf(string verifier) =>
        (Chains?.AllLinks ?? []).Where(link => link.IsActive && link.Kind == LinkKind.ReportsBack
                                                 && string.Equals(link.From, verifier, StringComparison.OrdinalIgnoreCase))
            .OrderBy(link => link.CreatedUtc).LastOrDefault()?.To;

    private bool ProjectIsStopped(string project) => _settings.Read().ProjectRunStates.ContainsKey(project);

    /// <summary>Queues a project's steps for after the board has been drawn, never while it is being built.</summary>
    private void TakeCheckSteps(ProjectFileReading reading, IReadOnlyList<PieceOfWork> pieces, CheckPlan plan)
    {
        if (!RunsChecksItself || !_launcher.CanLaunch || ProjectIsStopped(reading.Name) || _checksQueued.Contains(reading.Name))
        {
            return;
        }

        var verify = plan.VerifyKeys.Where(key => Board.Card(key) is { } card && !_checksTried.Contains(Tried(card))).ToList();
        var unnamed = pieces.Where(piece => piece is { IsCurrentPiece: true, Card: not null } && piece.Status.State == StageNodeState.HoldoutsNotRun
                                            && piece.PendingVerifiers.Count == 0 && !IsChecked(piece.CardKey)
                                            && Board.Card(piece.CardKey) is { } card && !_holdouts.IsNamed(card.Workstream))
            .Select(piece => piece.CardKey).ToList();
        var writerTry = WriterTry(reading.Name, unnamed);
        var writeBeside = plan.MakeWriter && !_checksTried.Contains(writerTry) ? unnamed.FirstOrDefault() : null;
        var startWriter = plan.StartWriter is { } idle && !_checksTried.Contains(writerTry) ? idle : null;

        if (verify.Count == 0 && writeBeside is null && startWriter is null)
        {
            return;
        }

        _checksQueued.Add(reading.Name);
        _checksDispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _checksQueued.Remove(reading.Name);

            // Looked at again: the owner may have stopped or paused something while this waited (review of the follow-on, M5).
            if (!RunsChecksItself || !_launcher.CanLaunch || ProjectIsStopped(reading.Name))
            {
                return;
            }

            if ((startWriter ?? writeBeside) is not null)
            {
                _checksTried.Add(writerTry);
            }

            if (startWriter is not null && Board.Card(startWriter) is { IsLive: false } writer && !IsHeld(writer)
                && !_launcher.InFlight.IsInFlight(writer.Key))
            {
                TheseusLog.Information("Starting the scenario writer {Writer} for {Project} itself: it stood and was not running", startWriter, reading.Name);
                StartMany([writer.Workstream]);
            }

            if (writeBeside is not null && WritersOf(reading.Name).Count == 0 && Board.Card(writeBeside) is { } build)
            {
                TheseusLog.Information("Making the scenario writer for {Project} itself: no scenario names {Builds}", reading.Name, string.Join(", ", unnamed));
                WriteScenarios(build);
            }

            var now = verify.Select(Board.Card).OfType<WorkstreamCardViewModel>()
                .Where(card => !card.IsLive && !IsHeld(card) && !IsChecked(card.Key) && WhyCannotVerify(card) is null).ToList();

            _checksTried.UnionWith(now.Select(Tried));

            if (now.Count > 0)
            {
                TheseusLog.Information("Making and starting verifiers itself for {Project}: {Builds}", reading.Name, string.Join(", ", now.Select(card => card.Key)));
                VerifyManyWithHoldouts([.. now.Select(card => card.Key)], quietly: true);
            }
        });
    }

    private static string WriterTry(string project, IEnumerable<string> unnamed) =>
        $"writer:{project}:{string.Join(",", unnamed.Order(StringComparer.OrdinalIgnoreCase))}";

    /// <summary>
    /// The waiting builds whose check Theseus will not start, one line for each reason: no holdout folder, no scenario
    /// naming them once the writer has had its go, and the like. Without them a build stuck there said nothing at all
    /// (review of P1, blocker 1). A build still being worked on, held by the owner, or about to be helped by a scenario
    /// writer Theseus is making or starting, is left out.
    /// </summary>
    private List<WaitItem> ChecksThatCannotRun(ProjectFileReading reading, IReadOnlyList<PieceOfWork> pieces, CheckPlan plan)
    {
        var stuck = pieces
            .Where(piece => piece is { IsCurrentPiece: true, Card: not null, CheckReported: null }
                            && piece.Status.State == StageNodeState.HoldoutsNotRun && piece.PendingVerifiers.Count == 0
                            && !IsChecked(piece.CardKey))
            .Select(piece => (Piece: piece, Card: Board.Card(piece.CardKey)))
            .Where(pair => pair.Card is { IsLive: false } card && !IsHeld(card))
            .Select(pair => (pair.Piece, Card: pair.Card!, Why: WhyCannotVerify(pair.Card!)))
            .Where(each => each.Why is not null)
            .ToList();

        if (stuck.Count == 0 || WritersOf(reading.Name).Any(card => card.IsLive || _launcher.InFlight.IsInFlight(card.Key)))
        {
            return [];
        }

        var unnamed = stuck.Where(each => !_holdouts.IsNamed(each.Card.Workstream)).Select(each => each.Card.Key);

        if ((plan.MakeWriter || plan.StartWriter is not null) && !_checksTried.Contains(WriterTry(reading.Name, unnamed)))
        {
            return [];
        }

        return
        [
            .. stuck.GroupBy(each => each.Why!, StringComparer.Ordinal).Select(group => new WaitItem(WaitKind.CheckCannotRun,
                group.Count() == 1
                    ? $"{group.First().Piece.Title}: its independent check cannot run. {Sentence(group.Key)}"
                    : $"{group.Count()} builds cannot have their independent check, {string.Join(", ", group.Select(each => each.Piece.Title))}. {Sentence(group.Key)}",
                [.. group.Select(each => each.Piece.Name)])
            {
                CardKey = group.First().Card.Key,
            }),
        ];
    }

    private static string Sentence(string text) => text.EndsWith('.') ? text : text + ".";

    // A build's check is tried once for the session that built it: reworked in a new session, it is checked again.
    private static string Tried(WorkstreamCardViewModel build) => $"verify:{build.Key}:{build.Workstream.LastSessionId}";
}
