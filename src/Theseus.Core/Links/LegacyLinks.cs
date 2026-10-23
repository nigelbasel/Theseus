using System.Text.Json.Nodes;

namespace Theseus.Core.Links;

/// <summary>
/// Reads links saved before 2026-10-23, when a link was either "spawned" or "feeds", as hand-overs
/// and report-backs. A spawned link is a hand-over. A feeds link is a report-back when its downstream
/// handed over to its upstream, and otherwise a hand-over. Where one pair of cards stood joined both
/// ways, the spawned half is removed and the feeds half, which holds the document, report and
/// acceptance, carries on.
/// </summary>
public static class LegacyLinks
{
    private const string Spawned = "Spawned";
    private const string Feeds = "Feeds";

    /// <summary>Rewrites old links in place. Returns how many it changed; links already in the new form are left alone.</summary>
    public static int Upgrade(JsonArray links)
    {
        ArgumentNullException.ThrowIfNull(links);

        var items = links.OfType<JsonObject>().Select(item => (Item: item, Kind: Kind(item))).ToList();
        var spawned = items.Where(entry => entry.Kind == Spawned).Select(entry => entry.Item).ToList();
        var feeds = items.Where(entry => entry.Kind == Feeds).Select(entry => entry.Item).ToList();
        var changed = 0;

        foreach (var (item, kind) in items)
        {
            if (kind == Feeds)
            {
                var returns = spawned.Any(parent => IsActive(parent) && IsActive(item)
                                                    && Same(parent, "from", item, "to") && Same(parent, "to", item, "from"));

                item["kind"] = returns ? nameof(LinkKind.ReportsBack) : nameof(LinkKind.HandsOver);
            }
            else if (kind == Spawned)
            {
                item["kind"] = nameof(LinkKind.HandsOver);

                if (feeds.FirstOrDefault(other => IsActive(other) && IsActive(item)
                                                  && Same(other, "from", item, "from") && Same(other, "to", item, "to")) is { } twin)
                {
                    item["removedUtc"] = twin["createdUtc"]?.DeepClone();
                }
            }
            else
            {
                continue;
            }

            item.Remove("brief");
            item.Remove("handOver");
            changed++;
        }

        return changed;
    }

    private static string? Kind(JsonObject item) =>
        item["kind"] is JsonValue value && value.TryGetValue<string>(out var kind) ? kind : null;

    private static bool IsActive(JsonObject item) => item["removedUtc"] is null;

    private static bool Same(JsonObject left, string leftName, JsonObject right, string rightName) =>
        string.Equals(left[leftName]?.GetValue<string>(), right[rightName]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
}
