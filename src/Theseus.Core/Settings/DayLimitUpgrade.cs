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

    private static readonly JsonNodeOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The settings text with the old day limits upgraded, or unchanged when it holds none.</summary>
    public static string Apply(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        if (!json.Contains("DayCostLimitUsd", StringComparison.OrdinalIgnoreCase) || JsonNode.Parse(json, CaseInsensitive) is not JsonObject root)
        {
            return json;
        }

        Upgrade(root["budgets"] as JsonObject);

        if (root["projectBudgets"] is JsonObject projects)
        {
            foreach (var project in projects)
            {
                Upgrade(project.Value as JsonObject);
            }
        }

        return root.ToJsonString();
    }

    private static void Upgrade(JsonObject? budget)
    {
        if (budget is null)
        {
            return;
        }

        foreach (var (old, renamed, oldDefault) in Renamed)
        {
            if (!budget.TryGetPropertyValue(old, out var value))
            {
                continue;
            }

            budget.Remove(old);

            if (!budget.ContainsKey(renamed) && value is JsonValue figure && figure.TryGetValue<decimal>(out var chosen) && chosen != oldDefault)
            {
                budget[renamed] = chosen;
            }
        }
    }
}
