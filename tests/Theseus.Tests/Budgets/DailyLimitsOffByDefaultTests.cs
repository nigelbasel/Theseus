using Theseus.Core.Budgets;
using Theseus.Core.Running;
using static Theseus.Tests.Running.RunRecords;

namespace Theseus.Tests.Budgets;

/// <summary>
/// The per-card and per-project daily cost limits are off unless set (Nigel, 2026-11-25: they silently refused every Start
/// again on the bug-fixing verifiers); the per-run ones went off the same day, and the breakers stay.
/// </summary>
[TestFixture]
public class DailyLimitsOffByDefaultTests
{
    [Test]
    public void TheDefaults_HaveNoDayLimit()
    {
        var defaults = BudgetSettings.Default;

        Assert.That((defaults.CardDayCostLimitUsd, defaults.ProjectDayCostLimitUsd), Is.EqualTo(((decimal?)null, (decimal?)null)));
    }

    [Test]
    public void WithTheDefaults_ACardAndProjectThatSpentALotToday_StillStart()
    {
        var verdict = DayBudget.Check(BudgetSettings.Default, [Run(cost: 100m), Run("other", cost: 100m)], "card", "proj", Noon, TimeZoneInfo.Utc);

        Assert.That(verdict.Allowed, Is.True);
        Assert.That(verdict.Limits!.CostUsd, Is.Null);
    }

    [Test]
    public void ADayLimitSwitchedOn_StillHolds()
    {
        var verdict = DayBudget.Check(BudgetSettings.Default with { ProjectDayCostLimitUsd = 60m },
            [Run("other", cost: 66.69m)], "card", "proj", Noon, TimeZoneInfo.Utc);

        Assert.That(verdict.Reached, Is.EqualTo(BudgetLimit.ProjectDay));
    }
}
