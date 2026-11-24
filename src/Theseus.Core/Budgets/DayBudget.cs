using Theseus.Core.Running;

namespace Theseus.Core.Budgets;

/// <summary>
/// Decides whether a headless run may start, from what the ledger says its card and project have spent today, and
/// gives it the least of the per-run limits and what is left of each day (objective 2.2). Runs still going are not
/// counted: their cost is only known when they end.
/// </summary>
public static class DayBudget
{
    /// <summary>Checks one card's next run.</summary>
    /// <param name="settings">The limits in force: the project's own, or the app's.</param>
    /// <param name="ledger">Every ended run.</param>
    /// <param name="workstream">The card's key.</param>
    /// <param name="project">The card's project, or null.</param>
    /// <param name="now">Now.</param>
    /// <param name="zone">The zone whose calendar day the day limits count in.</param>
    public static BudgetVerdict Check(
        BudgetSettings settings,
        IEnumerable<RunRecord> ledger,
        string workstream,
        string? project,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(ledger);
        ArgumentNullException.ThrowIfNull(zone);

        var today = TimeZoneInfo.ConvertTime(now, zone).Date;
        var todays = ledger.Where(run => TimeZoneInfo.ConvertTime(run.EndedUtc, zone).Date == today).ToList();

        var cardSpent = Spent(todays.Where(run => Same(run.Workstream, workstream)));
        if (cardSpent >= settings.CardDayCostLimitUsd)
        {
            return Refused(BudgetLimit.CardDay,
                $"Stopped at its limit: card's spend today {Money.Usd(cardSpent)} of {Money.Usd(settings.CardDayCostLimitUsd)} a day.");
        }

        var projectSpent = project is null ? 0m : Spent(todays.Where(run => Same(run.Project, project)));
        if (project is not null && projectSpent >= settings.ProjectDayCostLimitUsd)
        {
            return Refused(BudgetLimit.ProjectDay,
                $"Stopped at its limit: project {project}'s spend today {Money.Usd(projectSpent)} of {Money.Usd(settings.ProjectDayCostLimitUsd)} a day.");
        }

        var (cost, kind, full) = (settings.RunCostLimitUsd, BudgetLimit.RunCost, settings.RunCostLimitUsd);

        if (settings.CardDayCostLimitUsd - cardSpent < cost)
        {
            (cost, kind, full) = (settings.CardDayCostLimitUsd - cardSpent, BudgetLimit.CardDay, settings.CardDayCostLimitUsd);
        }

        if (project is not null && settings.ProjectDayCostLimitUsd - projectSpent < cost)
        {
            (cost, kind, full) = (settings.ProjectDayCostLimitUsd - projectSpent, BudgetLimit.ProjectDay, settings.ProjectDayCostLimitUsd);
        }

        return new BudgetVerdict(
            new RunLimits(cost, settings.RunTokenLimit, TimeSpan.FromMinutes(settings.RunTimeLimitMinutes), kind, full,
                settings.StopAtPercent),
            null,
            null);
    }

    private static BudgetVerdict Refused(BudgetLimit limit, string detail) => new(null, limit, detail);

    private static decimal Spent(IEnumerable<RunRecord> runs) => runs.Sum(run => run.CostUsd ?? 0m);

    private static bool Same(string? left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
