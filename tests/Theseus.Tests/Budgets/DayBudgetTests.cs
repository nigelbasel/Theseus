using Theseus.Core.Budgets;
using Theseus.Core.Running;
using static Theseus.Tests.Running.RunRecords;

namespace Theseus.Tests.Budgets;

[TestFixture]
public class DayBudgetTests
{
    private static readonly BudgetSettings Limits = new(RunCostLimitUsd: 5m, CardDayCostLimitUsd: 20m, ProjectDayCostLimitUsd: 60m);

    private static BudgetVerdict Check(params RunRecord[] ledger) =>
        DayBudget.Check(Limits, ledger, "card", "proj", Noon, TimeZoneInfo.Utc);

    [Test]
    public void AFreshCard_StartsWithThePerRunLimits()
    {
        var verdict = Check();

        Assert.That(verdict.Allowed, Is.True);
        Assert.That(verdict.Limits!.CostUsd, Is.EqualTo(5m));
        Assert.That(verdict.Limits.CostLimit, Is.EqualTo(BudgetLimit.RunCost));
    }

    [Test]
    public void ACardWithLittleLeftToday_GetsOnlyWhatIsLeft_AndTheDayLimitIsNamed()
    {
        var verdict = Check(Run(cost: 17.5m));

        Assert.That(verdict.Limits!.CostUsd, Is.EqualTo(2.5m));
        Assert.That(verdict.Limits.CostLimit, Is.EqualTo(BudgetLimit.CardDay));
        Assert.That(verdict.Limits.CostLimitValue, Is.EqualTo(20m));
    }

    [Test]
    public void ACardThatHasSpentItsDay_IsRefused_SayingTheSpendAndTheLimit()
    {
        var verdict = Check(Run(cost: 12m), Run(cost: 8.25m));

        Assert.That(verdict.Allowed, Is.False);
        Assert.That(verdict.Reached, Is.EqualTo(BudgetLimit.CardDay));
        Assert.That(verdict.Detail, Does.Contain("$20.25").And.Contain("$20.00"));
    }

    [Test]
    public void AProjectThatHasSpentItsDay_RefusesEveryCardInIt()
    {
        var verdict = Check(Run("other-1", cost: 30m), Run("other-2", cost: 30m));

        Assert.That(verdict.Reached, Is.EqualTo(BudgetLimit.ProjectDay));
    }

    [Test]
    public void YesterdaysSpend_DoesNotCountToday()
    {
        var verdict = Check(Run(cost: 19m, ended: Noon.AddDays(-1)));

        Assert.That(verdict.Limits!.CostUsd, Is.EqualTo(5m));
    }

    [Test]
    public void ARunThatProducedNoCost_CountsAsNothing()
    {
        var verdict = Check(Run(cost: null, outcome: RunOutcome.Crashed));

        Assert.That(verdict.Limits!.CostUsd, Is.EqualTo(5m));
    }

    [Test]
    public void ACardInNoProject_IsHeldOnlyByItsOwnDay()
    {
        var verdict = DayBudget.Check(Limits, [Run("other", project: null, cost: 100m)], "card", null, Noon, TimeZoneInfo.Utc);

        Assert.That(verdict.Allowed, Is.True);
    }
}
