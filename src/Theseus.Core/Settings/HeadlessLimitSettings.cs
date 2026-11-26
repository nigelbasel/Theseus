using System.Globalization;
using Theseus.Core.Breakers;
using Theseus.Core.Budgets;

namespace Theseus.Core.Settings;

/// <summary>
/// The headless runs' limits Options lists, each with its stated default (Nigel, 2026-11-24); every cost, token and time
/// limit is off unless set (Nigel, 2026-11-25). A project's own limits replace the budget ones for its cards, and are set
/// in its Project properties.
/// </summary>
public static class HeadlessLimitSettings
{
    private static readonly BudgetSettings Budget = BudgetSettings.Default;
    private const string Off = "Default off: type a figure to switch it on, or Off.";
    private static readonly BreakerSettings Breaker = BreakerSettings.Default;

    /// <summary>Every limit, in the order Options shows them.</summary>
    public static IReadOnlyList<HeadlessLimitSetting> All { get; } =
    [
        Whole("Headless runs at once", $"How many headless runs may go at once on this machine; the rest wait in order. Default {3}.",
            settings => settings.MaxHeadlessRuns, (settings, value) => settings with { MaxHeadlessRuns = value }),
        OptionalDollars("Cost per run", $"A run past it is stopped by Claude Code between calls, then resumed once to write its handoff. {Off}",
            settings => settings.Budgets.RunCostLimitUsd, (settings, value) => settings with { Budgets = settings.Budgets with { RunCostLimitUsd = value } }),
        OptionalWhole("Tokens per run", $"Input, output and cache-creation tokens; at {Budget.StopAtPercent}% the run is asked to hand over and stop. {Off}",
            settings => settings.Budgets.RunTokenLimit, (settings, value) => settings with { Budgets = settings.Budgets with { RunTokenLimit = value } }),
        OptionalWhole("Minutes per run", $"Wall-clock; at {Budget.StopAtPercent}% the run is asked to hand over and stop. {Off}",
            settings => settings.Budgets.RunTimeLimitMinutes, (settings, value) => settings with { Budgets = settings.Budgets with { RunTimeLimitMinutes = (int?)value } }),
        OptionalDollars("Cost per card per day", $"Once a card has spent it today, its headless starts are refused until tomorrow or until it is raised. {Off}",
            settings => settings.Budgets.CardDayCostLimitUsd, (settings, value) => settings with { Budgets = settings.Budgets with { CardDayCostLimitUsd = value } }),
        OptionalDollars("Cost per project per day", $"The same for all of a project's cards together. {Off}",
            settings => settings.Budgets.ProjectDayCostLimitUsd, (settings, value) => settings with { Budgets = settings.Budgets with { ProjectDayCostLimitUsd = value } }),
        Whole("Crashes in a row", $"Runs that crash or fail to start, in a row, before the card's breaker trips. Default {Breaker.Crashes}.",
            settings => settings.Breakers.Crashes, (settings, value) => settings with { Breakers = settings.Breakers with { Crashes = value } }),
        Whole("Runs without progress in a row", $"Runs with no new commit, record, tick or report, in a row, before the breaker trips. Default {Breaker.NoProgress}.",
            settings => settings.Breakers.NoProgress, (settings, value) => settings with { Breakers = settings.Breakers with { NoProgress = value } }),
        Whole("Same error in a row", $"Runs that fail with the same error, in a row, before the breaker trips. Default {Breaker.SameError}.",
            settings => settings.Breakers.SameError, (settings, value) => settings with { Breakers = settings.Breakers with { SameError = value } }),
        Whole("Minutes of grace past the time limit", $"How long a run may go past its time limit before it is interrupted and the breaker trips; only while Minutes per run is set. Default {Breaker.OverTimeGraceMinutes}.",
            settings => settings.Breakers.OverTimeGraceMinutes, (settings, value) => settings with { Breakers = settings.Breakers with { OverTimeGraceMinutes = value } }),
    ];

    private static HeadlessLimitSetting Whole(string label, string explanation, Func<TheseusSettings, long> get, Func<TheseusSettings, int, TheseusSettings> set) =>
        new(label, explanation, settings => get(settings).ToString("N0", CultureInfo.InvariantCulture),
            (settings, text) => int.TryParse(text.Replace(",", string.Empty, StringComparison.Ordinal).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
                ? set(settings, value)
                : null);

    // Every cost, token and time limit is off unless set (Nigel, 2026-11-25): Off, or nothing, switches one off again.
    private static HeadlessLimitSetting OptionalDollars(string label, string explanation, Func<TheseusSettings, decimal?> get, Func<TheseusSettings, decimal?, TheseusSettings> set) =>
        new(label, explanation, settings => get(settings) is { } limit ? Money.Usd(limit) : "Off",
            (settings, text) => text.Trim() is var typed && (typed.Length == 0 || typed.Equals("off", StringComparison.OrdinalIgnoreCase))
                ? set(settings, null)
                : decimal.TryParse(typed.TrimStart('$'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value > 0
                    ? set(settings, value)
                    : null);

    private static HeadlessLimitSetting OptionalWhole(string label, string explanation, Func<TheseusSettings, long?> get, Func<TheseusSettings, long?, TheseusSettings> set) =>
        new(label, explanation, settings => get(settings) is { } limit ? limit.ToString("N0", CultureInfo.InvariantCulture) : "Off",
            (settings, text) => text.Trim() is var typed && (typed.Length == 0 || typed.Equals("off", StringComparison.OrdinalIgnoreCase))
                ? set(settings, null)
                : long.TryParse(typed.Replace(",", string.Empty, StringComparison.Ordinal), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
                    ? set(settings, value)
                    : null);
}
