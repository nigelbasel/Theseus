using Theseus.Core.Abstractions;
using Theseus.Core.Breakers;
using Theseus.Core.Budgets;
using Theseus.Core.Domain;
using Theseus.Core.Launching;
using Theseus.Core.Running;
using Theseus.Core.Settings;

namespace Theseus.Tests.Running;

[TestFixture]
public class HeadlessRunnerTests
{
    private readonly List<FakeHeadlessProcess> _processes = [];
    private readonly List<(string Key, BreakerTrip Trip)> _trips = [];
    private string _folder = null!;
    private MovingClock _clock = null!;
    private RunLedger _ledger = null!;
    private TheseusSettings _settings = null!;
    private HeadlessRunner _runner = null!;

    [SetUp]
    public void Fresh()
    {
        _processes.Clear();
        _trips.Clear();
        _folder = Path.Combine(Path.GetTempPath(), "theseus-runner-" + Guid.NewGuid().ToString("N"));
        _clock = new MovingClock(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
        _ledger = new RunLedger(Path.Combine(_folder, "run-ledger.jsonl"));
        _settings = TheseusSettings.Default with { MaxHeadlessRuns = 1 };
        _runner = Runner();
    }

    [TearDown]
    public void Remove()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private HeadlessRunner Runner() =>
        new(_ledger, () => _settings, _clock, (request, line, exited) =>
            {
                var process = new FakeHeadlessProcess(request, line, exited);
                _processes.Add(process);

                return process;
            }, Path.Combine(_folder, "runs"), TimeZoneInfo.Utc)
        {
            Heads = _ => new Dictionary<string, string>(),
            Tripped = (key, trip) => _trips.Add((key, trip)),
        };

    private static Workstream Card(string key = "card", BreakerTrip? breaker = null) =>
        new(key, key.ToUpperInvariant(), "#208DD7", false, @"C:\work", [], "Carry on.", 0, "New", false, false, false, null, [], null,
            DateTimeOffset.UnixEpoch)
        {
            Project = "proj",
            Breaker = breaker,
        };

    private static HeadlessSubmission Submission(string key = "card") =>
        new(Card(key), new LaunchSpec("session-" + key, LaunchMode.FirstRun, @"C:\claude.exe", @"C:\work", key, "Read your brief.", []),
            RunReason.Owner, "Write your handoff and stop.");

    [Test]
    public void ASubmittedCard_StartsHeadless_WithItsPromptOnStdin_AndItsCostCapOnTheCommandLine()
    {
        Assert.That(_runner.Submit(Submission()), Is.Null);

        var process = _processes.Single();
        Assert.That(process.Request.Arguments, Does.Contain("-p").And.Contain("--max-budget-usd"));
        Assert.That(process.Input.Single(), Does.Contain("Read your brief."));
    }

    [Test]
    public void AnEndedRun_IsRecordedInTheLedger_WithItsMeasuredCost()
    {
        _runner.Submit(Submission());

        _processes[0].Completes(0.42m);

        var record = _ledger.Read().Single();
        Assert.That(record.CostUsd, Is.EqualTo(0.42m));
        Assert.That(record.Outcome, Is.EqualTo(RunOutcome.Completed));
        Assert.That(_runner.StateOf("card")!.Last, Is.EqualTo(record));
    }

    [Test]
    public void BeyondTheCap_RunsQueue_AndStartInOrderAsSlotsFree()
    {
        _runner.Submit(Submission("a"));
        Assert.That(_runner.Submit(Submission("b")), Is.EqualTo(1));
        Assert.That(_runner.Submit(Submission("c")), Is.EqualTo(2));
        Assert.That((_runner.Running, _runner.Queued), Is.EqualTo((1, 2)));

        _processes[0].Completes(0.1m);

        Assert.That(_processes, Has.Count.EqualTo(2));
        Assert.That(_processes[1].Request.Arguments, Does.Contain("session-b"));
        Assert.That(_runner.StateOf("c")!.QueuePosition, Is.EqualTo(1));
    }

    [Test]
    public void ACardWhoseDayIsSpent_IsRefused_SayingWhichLimit()
    {
        _settings = _settings with { Budgets = new BudgetSettings(CardDayCostLimitUsd: 1m) };
        _runner.Submit(Submission());
        _processes[0].Completes(1.5m);

        Assert.That(_runner.Refusal(Card()), Does.Contain("card's spend today $1.50 of $1.00"));
    }

    [Test]
    public void ATrippedCard_IsRefused_UntilReset()
    {
        var trip = new BreakerTrip(BreakerKind.Crashes, "Breaker tripped: 3 crashes in a row.", _clock.UtcNow);

        Assert.That(_runner.Refusal(Card(breaker: trip)), Does.Contain("3 crashes"));
        Assert.That(_runner.Refusal(Card()), Is.Null);
    }

    [Test]
    public void ThreeCrashesInARow_TripTheCardsBreaker()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            _runner.Submit(Submission());
            _processes[^1].Writes(StreamLines.Init);
            _processes[^1].LastError = "Error: boom";
            _processes[^1].Exits(1);
            _clock.Advance(TimeSpan.FromMinutes(1));
        }

        Assert.That(_trips.Single().Key, Is.EqualTo("card"));
        Assert.That(_trips.Single().Trip.Kind, Is.EqualTo(BreakerKind.Crashes));
    }

    [Test]
    public void ARunStoppedByClaudeCodesCostCap_IsResumedOnce_ToWriteItsHandoff()
    {
        _runner.Submit(Submission());
        _processes[0].Writes(StreamLines.Init);
        _processes[0].Writes(StreamLines.BudgetResult);
        _processes[0].Exits(1);

        Assert.That(_processes, Has.Count.EqualTo(2));
        var handoff = _processes[1];
        Assert.That(handoff.Request.Arguments, Does.Contain("--resume").And.Contain("session-card"));
        Assert.That(handoff.Input.Single(), Does.Contain("cost limit").And.Contain("Write your handoff and stop."));

        handoff.Completes(0.3m);

        Assert.That(_processes, Has.Count.EqualTo(2));
        Assert.That(_ledger.Read().Select(run => run.IsHandoffRun), Is.EqualTo(new[] { false, true }));
    }

    [Test]
    public void TheOwnersStop_IsWrittenIntoARunWhoseTurnIsGoing()
    {
        _runner.Submit(Submission());
        _processes[0].Writes(StreamLines.Init);

        Assert.That(_runner.AskToStop("card"), Is.True);
        Assert.That(_processes[0].Input.Last(), Does.Contain("Write your handoff and stop."));
    }

    [Test]
    public void StoppingAQueuedCard_TakesItOutOfTheQueue()
    {
        _runner.Submit(Submission("a"));
        _runner.Submit(Submission("b"));

        Assert.That(_runner.AskToStop("b"), Is.True);
        Assert.That(_runner.Queued, Is.EqualTo(0));
    }

    [Test]
    public void OpenInATab_InterruptsTheRun_AndCompletesWhenItHasExited_WithoutJudgingTheBreakers()
    {
        _settings = _settings with { Breakers = new BreakerSettings(NoProgress: 1) };
        _runner.Submit(Submission());
        _processes[0].Writes(StreamLines.Init);

        var exited = _runner.InterruptAsync("card");
        Assert.That(exited.IsCompleted, Is.False);
        Assert.That(_processes[0].Input.Last(), Does.Contain("interrupt"));

        _processes[0].Exits(0);

        Assert.That(exited.IsCompleted, Is.True);
        Assert.That(_trips, Is.Empty);
    }

    [Test]
    public void ACallToARecordTool_CountsAsProgress()
    {
        _runner.Submit(Submission());
        _runner.ToolCalled("session-card", "tick_criterion");
        _processes[0].Completes(0.1m);

        Assert.That(_ledger.Read().Single().MadeProgress, Is.True);
    }

    [Test]
    public void ACallToAnotherTool_IsNotProgress()
    {
        _runner.Submit(Submission());
        _runner.ToolCalled("session-card", "related");
        _processes[0].Completes(0.1m);

        Assert.That(_ledger.Read().Single().MadeProgress, Is.False);
    }

    [Test]
    public void EveryLine_IsKeptInTheRunsLog()
    {
        _runner.Submit(Submission());
        _processes[0].Completes(0.1m);

        var log = _runner.StateOf("card")!.LogPath!;
        Assert.That(File.ReadAllLines(log), Has.Length.EqualTo(2));
    }

    [Test]
    public void AStopThatArrivesWhileAQueuedRunIsStarting_CancelsTheStart_AndFreesTheSlot()
    {
        _runner.Submit(Submission("a"));
        _runner.Submit(Submission("b"));
        var stopped = false;
        _runner.Heads = folders =>
        {
            // Prepare reads the repositories' HEADs before the process starts: the stop lands then.
            if (_processes.Count == 1 && !stopped)
            {
                stopped = _runner.AskToStop("b");
            }

            return new Dictionary<string, string>();
        };

        _processes[0].Completes(0.1m);

        Assert.That(stopped, Is.True);
        Assert.That(_processes, Has.Count.EqualTo(1));
        Assert.That((_runner.Running, _runner.Queued), Is.EqualTo((0, 0)));
    }

    [Test]
    public void AnOpenInATabWhileTheHandoffResumeIsStarting_CancelsTheResume()
    {
        _runner.Submit(Submission());
        _processes[0].Writes(StreamLines.Init);
        _processes[0].Writes(StreamLines.BudgetResult);
        Task? opened = null;
        _runner.Heads = folders =>
        {
            opened ??= _runner.InterruptAsync("card");

            return new Dictionary<string, string>();
        };

        _processes[0].Exits(1);

        Assert.That(opened!.IsCompleted, Is.True);
        Assert.That(_processes, Has.Count.EqualTo(1));
        Assert.That(_runner.Holds("card"), Is.False);
    }

    [Test]
    public void ALedgerThatCannotBeWritten_StillFreesTheSlot_AndReleasesOpenInATab()
    {
        _runner = new HeadlessRunner(new FailingLedger(), () => _settings, _clock, (request, line, exited) =>
            {
                var process = new FakeHeadlessProcess(request, line, exited);
                _processes.Add(process);

                return process;
            }, Path.Combine(_folder, "runs"), TimeZoneInfo.Utc)
        {
            Heads = _ => new Dictionary<string, string>(),
        };
        _runner.Submit(Submission());

        _processes[0].Completes(0.1m);

        Assert.That(_runner.Holds("card"), Is.False);
        Assert.That(_runner.Running, Is.EqualTo(0));
    }

    [Test]
    public void AfterACostCapAndItsHandoff_TheCardShowsTheRunThatHitTheLimit()
    {
        _runner.Submit(Submission());
        _processes[0].Writes(StreamLines.Init);
        _processes[0].Writes(StreamLines.BudgetResult);
        _processes[0].Exits(1);
        _processes[1].Completes(0.3m);

        var last = _runner.StateOf("card")!.Last!;
        Assert.That(last.Outcome, Is.EqualTo(RunOutcome.StoppedAtLimit));
        Assert.That(last.IsHandoffRun, Is.False);
    }

    [Test]
    public void ARaisedCap_StartsTheQueuedRunsAtTheNextSubmission()
    {
        _runner.Submit(Submission("a"));
        _runner.Submit(Submission("b"));
        _settings = _settings with { MaxHeadlessRuns = 3 };

        Assert.That(_runner.Submit(Submission("c")), Is.Null);
        Assert.That(_processes, Has.Count.EqualTo(3));
    }

    private sealed class FailingLedger : IRunLedger
    {
        public void Append(RunRecord record) => throw new IOException("The ledger is locked by another process.");

        public IReadOnlyList<RunRecord> Read() => [];
    }

    private sealed class MovingClock(DateTimeOffset start) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; private set; } = start;

        public void Advance(TimeSpan by) => UtcNow += by;
    }
}
