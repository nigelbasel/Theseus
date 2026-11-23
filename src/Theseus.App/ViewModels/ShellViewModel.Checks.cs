using System.Windows.Threading;
using Theseus.Core.Diagnostics;
using Theseus.Core.Domain;
using Theseus.Core.Projects;

namespace Theseus.App.ViewModels;

/// <summary>
/// The independent check run without the owner's press (git-22, 2026-11-23: nothing Theseus makes waits on him): each
/// project's waiting builds get their verifier, or their scenario writer, as <see cref="CheckSteps"/> decides, and the
/// Waits line says what is under way. Clear up verifiers… stays a press: archiving is his.
/// </summary>
public sealed partial class ShellViewModel
{
    // Each build and each project's writer is tried once while Theseus runs, so one that cannot be made is not tried on
    // every refresh.
    private readonly HashSet<string> _checksTried = new(StringComparer.OrdinalIgnoreCase);
    private bool _checksQueued;

    /// <summary>
    /// Whether Theseus makes and starts the checks itself. Set by the app alone: a view model built by a test or a tool that
    /// reads the window never starts a session.
    /// </summary>
    public bool RunsChecksItself { get; set; }

    /// <summary>Each project's steps as last worked out, by project, whether or not they were taken: read by tools.</summary>
    public IReadOnlyDictionary<string, CheckPlan> CheckPlans => _checkPlans;

    private readonly Dictionary<string, CheckPlan> _checkPlans = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The steps for a project's pieces, from the holdout folders and the board as last refreshed.</summary>
    private CheckPlan CheckPlanOf(ProjectFileReading reading, IReadOnlyList<PieceOfWork> pieces)
    {
        var writers = Board.Cards.Where(card => card.Workstream is { Role: WorkstreamRole.ScenarioWriter, IsArchived: false }
                                                && string.Equals(WriterProject(card.Workstream), reading.Name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var location = _holdouts.LocationOfProject(reading.Name);

        var plan = CheckSteps.For(pieces, piece => piece.Card is not null && Board.Card(piece.CardKey) is { } card && _holdouts.IsNamed(card.Workstream),
            writerStands: writers.Count > 0,
            writerRunning: writers.Any(card => card.IsLive || _launcher.InFlight.IsInFlight(card.Key)),
            canWrite: !string.IsNullOrWhiteSpace(location.Folder) && !location.IsMissing,
            idleWriter: writers.FirstOrDefault(card => !card.IsLive && !_launcher.InFlight.IsInFlight(card.Key))?.Key);
        _checkPlans[reading.Name] = plan;

        return plan;
    }

    /// <summary>Takes a project's steps after the board has been drawn, never while it is being built.</summary>
    private void TakeCheckSteps(ProjectFileReading reading, IReadOnlyList<PieceOfWork> pieces, CheckPlan plan)
    {
        // A project the owner stopped starts nothing until he starts it.
        if (!RunsChecksItself || _checksQueued || !_launcher.CanLaunch || _settings.Read().ProjectRunStates.ContainsKey(reading.Name))
        {
            return;
        }

        var verify = plan.VerifyKeys.Where(key => !_checksTried.Contains(key)
                                                  && Board.Card(key) is { } card && WhyCannotVerify(card) is null).ToList();
        var writeFor = plan.MakeWriter && !_checksTried.Contains($"writer:{reading.Name}")
            ? pieces.FirstOrDefault(piece => piece is { IsCurrentPiece: true, Card: not null } && piece.Status.State == StageNodeState.HoldoutsNotRun
                                             && piece.PendingVerifiers.Count == 0)?.CardKey
            : null;

        // A standing writer is started, and so aimed at the project's holdout folder of today, rather than left waiting.
        var startWriter = plan.StartWriter is { } idle && _checksTried.Add($"writer:{reading.Name}") ? idle : null;

        if (verify.Count == 0 && writeFor is null && startWriter is null)
        {
            return;
        }

        _checksTried.UnionWith(verify);

        if (writeFor is not null)
        {
            _checksTried.Add($"writer:{reading.Name}");
        }

        _checksQueued = true;
        Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            _checksQueued = false;

            if (startWriter is not null && Board.Card(startWriter) is { } writer)
            {
                TheseusLog.Information("Starting the scenario writer {Writer} for {Project} itself: it stood and was not running", startWriter, reading.Name);
                StartMany([writer.Workstream]);
            }

            if (writeFor is not null && Board.Card(writeFor) is { } build)
            {
                TheseusLog.Information("Making the scenario writer for {Project} itself: no scenario names some of its waiting builds", reading.Name);
                WriteScenarios(build);
            }

            if (verify.Count > 0)
            {
                TheseusLog.Information("Making and starting verifiers itself for {Project}: {Builds}", reading.Name, string.Join(", ", verify));
                VerifyManyWithHoldouts(verify);
            }
        });
    }
}
