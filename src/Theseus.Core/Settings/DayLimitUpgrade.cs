using System.Text.Json.Nodes;

namespace Theseus.Core.Settings;

/// <summary>
/// The daily cost limits went off by default (Nigel, 2026-11-25), but every settings file written before then holds $20
/// and $60 only because the whole budget is written back, defaults and all. Those are dropped as the file is read; a figure
/// someone chose is carried over under the new name. The new name is what makes this happen once: a limit set to $20
/// afterwards is never taken for the old default.
/// </summary>
public static class DayLimitUpgrade
{
    private static readonly (string Old, string New, decimal OldDefault)[] Renamed =
    [
        ("cardDayCostLimitUsd", "cardDayLimitUsd", 20.00m),
        ("projectDayCostLimitUsd", "projectDayLimitUsd", 60.00m),
    ];

    /// <summary>The settings text with the old day limits upgraded, or unchanged when it holds none.</summary>
    public static string Apply(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (!json.Contains("DayCostLimitUsd", StringComparison.OrdinalIgnoreCase) || JsonNode.Parse(json) is not JsonObject root)
        {
            return json;
        }

        Upgrade(Named(root, "budgets") as JsonObject);

        if (Named(root, "projectBudgets") is JsonObject projects)
        {
            foreach (var project in projects)
            {
                Upgrade(project.Value as JsonObject);
            }
        }

        return root.ToJsonString();
    }

    // Matched as the serialiser reads them, ignoring case, but without a case-insensitive object: two project names that
    // differ only in case would make one throw.
    private static JsonNode? Named(JsonObject parent, string name) =>
        parent.FirstOrDefault(property => property.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;

    private static void Upgrade(JsonObject? budget)
    {
        if (budget is null)
        {
            return;
        }

        foreach (var (old, renamed, oldDefault) in Renamed)
        {
            if (budget.FirstOrDefault(property => property.Key.Equals(old, StringComparison.OrdinalIgnoreCase)) is not { Key: { } found, Value: var value })
            {
                continue;
            }

            budget.Remove(found);

            if (Named(budget, renamed) is null && value is JsonValue figure && figure.TryGetValue<decimal>(out var chosen) && chosen != oldDefault)
            {
                budget[renamed] = chosen;
            }
        }
    }
}
