using System.Globalization;
using System.Text.Json;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Usage;

/// <summary>
/// Reads the usage endpoint's response. The endpoint is undocumented and carries a shifting set of
/// named windows, so parsing takes the <c>limits</c> array as the contract, falls back to the two
/// named windows when it is absent, and ignores anything it does not recognise instead of failing.
/// </summary>
public static class UsageResponseParser
{
    /// <summary>Parses a usage response, returning no reading rather than throwing.</summary>
    public static AccountUsage Parse(string? json, DateTimeOffset retrievedUtc)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return AccountUsage.None;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
            {
                return AccountUsage.None;
            }

            var limits = FromLimitsArray(root).ToList();

            if (limits.Count == 0)
            {
                limits.AddRange(FromNamedWindows(root));
            }

            return limits.Count == 0 ? AccountUsage.None : new AccountUsage(limits, retrievedUtc);
        }
        catch (JsonException exception)
        {
            TheseusLog.Warning("The usage response did not parse: {Reason}", exception.Message);

            return AccountUsage.None;
        }
    }

    private static IEnumerable<UsageLimit> FromLimitsArray(JsonElement root)
    {
        if (!root.TryGetProperty("limits", out var limits) || limits.ValueKind != JsonValueKind.Array)
        {
            yield break;
        }

        foreach (var entry in limits.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var kind = Text(entry, "kind");
            if (string.IsNullOrEmpty(kind))
            {
                continue;
            }

            yield return new UsageLimit(
                kind,
                Text(entry, "group") ?? string.Empty,
                Number(entry, "percent") ?? 0,
                Text(entry, "severity") ?? "normal",
                Instant(entry, "resets_at"),
                ScopeLabel(entry),
                entry.TryGetProperty("is_active", out var active) && active.ValueKind == JsonValueKind.True);
        }
    }

    private static IEnumerable<UsageLimit> FromNamedWindows(JsonElement root)
    {
        foreach (var (property, kind, group) in new[]
                 {
                     ("five_hour", "session", "session"),
                     ("seven_day", "weekly_all", "weekly"),
                 })
        {
            if (!root.TryGetProperty(property, out var window) || window.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (Number(window, "utilization") is not { } utilization)
            {
                continue;
            }

            yield return new UsageLimit(kind, group, utilization, "normal",
                Instant(window, "resets_at"), null, IsActive: false);
        }
    }

    private static string? ScopeLabel(JsonElement entry)
    {
        if (!entry.TryGetProperty("scope", out var scope) || scope.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (scope.TryGetProperty("model", out var model) && model.ValueKind == JsonValueKind.Object)
        {
            return Text(model, "display_name") ?? Text(model, "id");
        }

        return Text(scope, "surface");
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? Number(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    private static DateTimeOffset? Instant(JsonElement element, string name)
    {
        var text = Text(element, name);

        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;
    }
}
