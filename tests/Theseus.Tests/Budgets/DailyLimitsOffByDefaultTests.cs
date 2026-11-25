using Theseus.Core.Budgets;
using Theseus.Core.Running;
using static Theseus.Tests.Running.RunRecords;

namespace Theseus.Tests.Budgets;

/// <summary>
/// The per-card and per-project daily cost limits are off unless set; the per-run limits and the breaker stay (Nigel,
/// 2026-11-25: they silently refused every Start again on the bug-fixing verifiers).
/// </summary>
[TestFixture]
public class DailyLimitsOffByDefaultTests
{
    [Test]
    public void TheDefaults_HaveNoDayLimit_AndKeepThePerRunLimits()
    {
        var defaults = BudgetSettings.Default;

        Assert.That((defaults.CardDayCostLimitUsd, defaults.ProjectDayCostLimitUsd), Is.EqualTo(((decimal?)null, (decimal?)null)));
        Assert.That((defaults.RunCostLimitUsd, defaults.RunTokenLimit, defaults.RunTimeLimitMinutes), Is.EqualTo((5.00m, 3_000_000L, 120)));
    }

    [Test]
    public void WithTheDefaults_ACardAndProjectThatSpentALotToday_StillStart_WithThePerRunLimits()
    {
        var verdict = DayBudget.Check(BudgetSettings.Default, [Run(cost: 100m), Run("other", cost: 100m)], "card", "proj", Noon, TimeZoneInfo.Utc);

        Assert.That(verdict.Allowed, Is.True);
        Assert.That((verdict.Limits!.CostUsd, verdict.Limits.CostLimit), Is.EqualTo((5m, BudgetLimit.RunCost)));
    }

    [Test]
    public void ADayLimitSwitchedOn_StillHolds()
    {
        var verdict = DayBudget.Check(BudgetSettings.Default with { ProjectDayCostLimitUsd = 60m },
            [Run("other", cost: 66.69m)], "card", "proj", Noon, TimeZoneInfo.Utc);

        Assert.That(verdict.Reached, Is.EqualTo(BudgetLimit.ProjectDay));
    }
}
