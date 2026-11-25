using Theseus.Core.Budgets;
using Theseus.Core.Running;
using Theseus.Tests.Running;
using static Theseus.Tests.Running.RunRecords;

namespace Theseus.Tests.Budgets;

/// <summary>
/// The per-run cost, token and time limits are off unless set, as the daily ones are (Nigel, 2026-11-25: "we dont want to
/// have any limits turned on"): the $5 cap did not hold a $19.52 turn, and the two-hour limit ended verifiers after real
/// work with no report.
/// </summary>
[TestFixture]
public class RunLimitsOffByDefaultTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 25, 8, 0, 0, TimeSpan.Zero);

    [Test]
    public void TheDefaults_HaveNoPerRunLimit()
    {
        var defaults = BudgetSettings.Default;

        Assert.That((defaults.RunCostLimitUsd, defaults.RunTokenLimit, defaults.RunTimeLimitMinutes),
            Is.EqualTo(((decimal?)null, (long?)null, (int?)null)));
    }

    [Test]
    public void WithTheDefaults_ARunStartsWithNoCostCap_NoTokenLimit_AndNoTimeLimit()
    {
        var verdict = DayBudget.Check(BudgetSettings.Default, [Run(cost: 100m)], "card", "proj", Noon, TimeZoneInfo.Utc);

        Assert.That(verdict.Allowed, Is.True);
        Assert.That((verdict.Limits!.CostUsd, verdict.Limits.Tokens, verdict.Limits.Time),
            Is.EqualTo(((decimal?)null, (long?)null, (TimeSpan?)null)));
    }

    [Test]
    public void APerRunLimitSwitchedOn_StillHolds()
    {
        var verdict = DayBudget.Check(BudgetSettings.Default with { RunCostLimitUsd = 5m, RunTimeLimitMinutes = 120 }, [], "card", "proj",
            Noon, TimeZoneInfo.Utc);

        Assert.That((verdict.Limits!.CostUsd, verdict.Limits.CostLimit, verdict.Limits.Time),
            Is.EqualTo(((decimal?)5m, BudgetLimit.RunCost, (TimeSpan?)TimeSpan.FromHours(2))));
    }

    [Test]
    public void ADayLimitSwitchedOn_CapsARunThatHasNoPerRunLimit()
    {
        var verdict = DayBudget.Check(BudgetSettings.Default with { CardDayCostLimitUsd = 20m }, [Run(cost: 12m)], "card", "proj",
            Noon, TimeZoneInfo.Utc);

        Assert.That((verdict.Limits!.CostUsd, verdict.Limits.CostLimit), Is.EqualTo(((decimal?)8m, BudgetLimit.CardDay)));
    }

    [Test]
    public void ARunWithNoLimits_IsNeverAskedToStop_NorInterrupted_HoweverLongItGoes()
    {
        var input = new RecordingInput();
        var limits = DayBudget.Check(BudgetSettings.Default, [], "card", "proj", Start, TimeZoneInfo.Utc).Limits!;
        var run = new HeadlessRun(new HeadlessRunStart(Guid.NewGuid(), "card", "proj", "session", RunReason.Owner, limits, "Stop."), input, Start);
        run.Begin("Read your brief.");
        run.Observe(StreamLines.Init);

        foreach (var hours in new[] { 2, 3, 10 })
        {
            Assert.That(run.Tick(Start.AddHours(hours)), Is.EqualTo(HeadlessRunAction.None));
        }

        Assert.That(run.StopAsked, Is.False);
        Assert.That(run.Ended(0, Start.AddHours(10), madeProgress: true).Outcome, Is.Not.EqualTo(RunOutcome.OverTime));
    }
}
