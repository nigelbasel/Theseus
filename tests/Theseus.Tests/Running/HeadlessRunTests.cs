using Theseus.Core.Budgets;
using Theseus.Core.Running;

namespace Theseus.Tests.Running;

[TestFixture]
public class HeadlessRunTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 24, 22, 0, 0, TimeSpan.Zero);

    private static readonly RunLimits Limits = new(5m, 1_000, TimeSpan.FromMinutes(100), BudgetLimit.RunCost, 5m, 90);

    private RecordingInput _input = null!;

    [SetUp]
    public void Fresh() => _input = new RecordingInput();

    private HeadlessRun Run(RunLimits? limits = null) =>
        new(new HeadlessRunStart(Guid.NewGuid(), "card", "proj", "session", RunReason.Owner, limits ?? Limits, "Write your handoff and stop."),
            _input, Start);

    [Test]
    public void Begin_WritesThePromptAsTheFirstUserMessage()
    {
        Run().Begin("Read your brief.");

        Assert.That(_input.Lines.Single(), Is.EqualTo(RunStreamParser.UserMessage("Read your brief.")));
    }

    [Test]
    public void TheFirstResult_ClosesStdin_ButTheRunGoesOnUntilItsProcessExits()
    {
        var run = Run();
        run.Observe(StreamLines.Init);
        run.Observe(StreamLines.Result(0.09m));

        Assert.That(_input.Closes, Is.EqualTo(1));
        Assert.That(run.IsInputOpen, Is.False);
        Assert.That(run.CostUsd, Is.EqualTo(0.09m));
    }

    [Test]
    public void ACostThatGrowsOverTwoTurns_IsTheLastResultsCost_AndTheTokensAreSummed()
    {
        var run = Run();
        run.Observe(StreamLines.Init);
        run.Observe(StreamLines.Result(0.25m, output: 4));
        run.Observe(StreamLines.Result(0.26m, output: 6));

        var record = run.Ended(0, Start.AddMinutes(1), madeProgress: true);

        Assert.That(record.CostUsd, Is.EqualTo(0.26m));
        Assert.That(record.Tokens.Output, Is.EqualTo(10));
        Assert.That(record.Outcome, Is.EqualTo(RunOutcome.Completed));
    }

    [Test]
    public void TokensDuringATurn_AreCountedOncePerMessage()
    {
        var run = Run();
        run.Observe(StreamLines.Text("m1", "first", output: 5));
        run.Observe(StreamLines.Tool("m1", "ls", output: 5));
        run.Observe(StreamLines.Tool("m2", "git status", output: 7));

        Assert.That(run.Tokens.Output, Is.EqualTo(12));
        Assert.That(run.LastLine, Is.EqualTo("Bash: git status"));
    }

    [Test]
    public void ARunEndedByTheCostCap_KeepsTheTokensItsCallsReported_SinceThatResultReportsNone()
    {
        var run = Run();
        run.Observe(StreamLines.Init);
        run.Observe(StreamLines.Text("m1", "1 2 3", output: 50, creation: 40_000));
        run.Observe("""{"type":"result","subtype":"error_max_budget_usd","is_error":true,"total_cost_usd":0.35,"usage":{"input_tokens":0,"output_tokens":0,"cache_creation_input_tokens":0,"cache_read_input_tokens":0}}""");

        Assert.That(run.Ended(1, Start, false).Tokens.Counted, Is.EqualTo(40_052));
    }

    [Test]
    public void ATurnCutShortAfterAnEarlierResult_KeepsBothTurnsTokens()
    {
        var run = Run();
        run.Observe(StreamLines.Init);
        run.Observe(StreamLines.Result(0.1m, output: 4));
        run.Observe(StreamLines.Text("m9", "more", output: 30));

        Assert.That(run.Ended(0, Start, false).Tokens.Output, Is.EqualTo(34));
    }

    [Test]
    public void NearingItsTokenLimit_TheRunIsAskedToHandOverOnce_NamingTheLimit()
    {
        var run = Run();
        run.Observe(StreamLines.Init);
        run.Observe(StreamLines.Text("m1", "working", output: 905));

        run.Tick(Start.AddMinutes(1));
        run.Tick(Start.AddMinutes(2));

        var stops = _input.Lines.Where(line => line.Contains("handoff", StringComparison.Ordinal)).ToList();
        Assert.That(stops, Has.Count.EqualTo(1));
        Assert.That(stops[0], Does.Contain("run tokens"));
        Assert.That(run.StoppedAt, Is.EqualTo(BudgetLimit.RunTokens));
    }

    [Test]
    public void NearingItsTimeLimit_TheRunIsAskedToHandOver()
    {
        var run = Run();
        run.Observe(StreamLines.Init);

        run.Tick(Start.AddMinutes(89));
        Assert.That(run.StopAsked, Is.False);

        run.Tick(Start.AddMinutes(90));
        Assert.That(run.StoppedAt, Is.EqualTo(BudgetLimit.RunTime));
        Assert.That(run.Ended(0, Start.AddMinutes(95), true).LimitDetail, Is.EqualTo("Stopped at its limit: run time 90 minutes of 100."));
    }

    [Test]
    public void PastItsTimeAndGrace_TheRunIsInterrupted_AndEndsOverTime()
    {
        var run = Run();
        run.Observe(StreamLines.Init);

        run.Tick(Start.AddMinutes(116));

        Assert.That(_input.Lines.Last(), Does.Contain("interrupt"));
        Assert.That(run.Ended(0, Start.AddMinutes(117), false).Outcome, Is.EqualTo(RunOutcome.OverTime));
    }

    [Test]
    public void ARunThatWillNotExitAfterItsInterrupt_IsEndedTwoMinutesLater()
    {
        var run = Run();
        run.Observe(StreamLines.Init);
        run.Tick(Start.AddMinutes(116));

        Assert.That(run.Tick(Start.AddMinutes(117)), Is.EqualTo(HeadlessRunAction.None));
        Assert.That(run.Tick(Start.AddMinutes(118)), Is.EqualTo(HeadlessRunAction.EndProcess));
    }

    [Test]
    public void ClaudeCodesCostCap_EndsTheRunStoppedAtItsCostLimit()
    {
        var run = Run();
        run.Observe(StreamLines.Init);
        run.Observe(StreamLines.BudgetResult);

        var record = run.Ended(1, Start.AddMinutes(1), false);

        Assert.That(record.Outcome, Is.EqualTo(RunOutcome.StoppedAtLimit));
        Assert.That(record.Limit, Is.EqualTo(BudgetLimit.RunCost));
        Assert.That(record.LimitDetail, Is.EqualTo("Stopped at its limit: run cost $0.21 of $5.00."));
    }

    [Test]
    public void ACostCapFromTheCardsDay_SaysSo()
    {
        var run = Run(Limits with { CostUsd = 2m, CostLimit = BudgetLimit.CardDay, CostLimitValue = 20m });
        run.Observe(StreamLines.Init);
        run.Observe(StreamLines.BudgetResult);

        Assert.That(run.Ended(1, Start, false).LimitDetail, Does.Contain("card's spend today reached its $20.00 a day"));
    }

    [Test]
    public void NoSessionWithinAMinute_IsAFailedStart_AndTheProcessIsEnded()
    {
        var run = Run();

        Assert.That(run.Tick(Start.AddSeconds(59)), Is.EqualTo(HeadlessRunAction.None));
        Assert.That(run.Tick(Start.AddSeconds(60)), Is.EqualTo(HeadlessRunAction.EndProcess));
        Assert.That(run.Ended(-1, Start.AddSeconds(61), false).Outcome, Is.EqualTo(RunOutcome.FailedStart));
    }

    [Test]
    public void AnExitWithNoResult_IsACrash_WithItsLastErrorLine()
    {
        var run = Run();
        run.Observe(StreamLines.Init);

        var record = run.Ended(3, Start.AddMinutes(1), false, "Error: something broke\nat line 2");

        Assert.That(record.Outcome, Is.EqualTo(RunOutcome.Crashed));
        Assert.That(record.Error, Is.EqualTo("Error: something broke"));
    }

    [Test]
    public void AnErrorResult_IsAFailure_WithItsText()
    {
        var run = Run();
        run.Observe(StreamLines.Init);
        run.Observe(StreamLines.Result(0.01m, "error_during_execution", error: true, result: "API Error: 500"));

        var record = run.Ended(1, Start, false);

        Assert.That(record.Outcome, Is.EqualTo(RunOutcome.Failed));
        Assert.That(record.Error, Is.EqualTo("API Error: 500"));
    }

    [Test]
    public void TheOwnersStop_ReachesARunWhoseTurnIsGoing_ButNotOneWhoseTurnHasEnded()
    {
        var run = Run();
        run.Observe(StreamLines.Init);

        Assert.That(run.AskToStop(), Is.True);
        run.Observe(StreamLines.Result(0.1m));
        Assert.That(run.AskToStop(), Is.False);
    }

    [Test]
    public void OpenInATab_Interrupts_AndTheRunEndsInterrupted()
    {
        var run = Run();
        run.Observe(StreamLines.Init);

        run.Interrupt(RunOutcome.Interrupted, Start.AddMinutes(3));

        Assert.That(_input.Closes, Is.EqualTo(1));
        Assert.That(run.Ended(0, Start.AddMinutes(3), false).Outcome, Is.EqualTo(RunOutcome.Interrupted));
    }
}
