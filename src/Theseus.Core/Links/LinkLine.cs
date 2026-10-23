namespace Theseus.Core.Links;

/// <summary>
/// The one line a card gives its links, in words. Readiness comes first, then lineage, because
/// whether a card can be started is what a glance at the board is for. Exact detail goes in the
/// tooltip. The lifecycle glyph is never touched: link state has its own line.
/// </summary>
public static class LinkLine
{
    private const int NamesShown = 2;

    /// <summary>What a card's link line says.</summary>
    public static LinkLineText For(string key, LinkStatus status)
    {
        ArgumentNullException.ThrowIfNull(status);

        var links = status.For(key);

        if (links.Count == 0)
        {
            return LinkLineText.None;
        }

        var parts = new List<string>();
        var readiness = status.ReadinessOf(key);

        if (readiness == CardReadiness.Ready)
        {
            parts.Add("Ready");
        }
        else if (readiness == CardReadiness.Waiting)
        {
            parts.Add($"⧗ waits on {Names(status.WaitingOn(key), status)}");
        }

        var inputStates = status.Inputs(key).Select(status.StateOf).ToList();
        var toConfirm = inputStates.Count(state => state == LinkState.Reported);

        if (toConfirm > 0)
        {
            parts.Add($"? {toConfirm} to confirm");
        }

        if (inputStates.Contains(LinkState.ReportLost))
        {
            parts.Add("! report lost");
        }

        var parent = links.FirstOrDefault(link => link.Kind == LinkKind.Spawned && Same(link.To, key));
        var children = links.Where(link => link.Kind == LinkKind.Spawned && Same(link.From, key)).ToList();
        var feeds = links.Where(link => link.Kind == LinkKind.Feeds && Same(link.From, key)).ToList();

        if (parent is not null)
        {
            parts.Add($"↑ from {Title(parent.From, status)}");
        }

        if (readiness != CardReadiness.Waiting)
        {
            if (children.Count > 0)
            {
                parts.Add(children.Count == 1 ? "⑂ 1 child" : $"⑂ {children.Count} children");
            }

            if (children.Where(child => child.HandOver is not null).Select(child => child.To).ToList() is { Count: > 0 } handedTo)
            {
                parts.Add($"handed over to {Names(handedTo, status)}");
            }

            if (feeds.Count > 0)
            {
                parts.Add($"→ {Names([.. feeds.Select(link => link.To)], status)}");
            }
        }

        var outputStates = feeds.Select(status.StateOf).ToList();

        if (outputStates.Contains(LinkState.Reported))
        {
            parts.Add("reported");
        }
        else if (outputStates.Any(state => state is LinkState.NoReportYet or LinkState.ReportLost))
        {
            parts.Add("no report yet");
        }

        if (links.Any(link => status.StateOf(link) == LinkState.CardMissing))
        {
            parts.Add("linked card missing");
        }

        return new LinkLineText(string.Join(" · ", parts), Tooltip(key, links, status),
            readiness == CardReadiness.Ready);
    }

    private static string Tooltip(string key, IReadOnlyList<Link> links, LinkStatus status) =>
        string.Join(Environment.NewLine, links.Select(link => Describe(key, link, status)));

    private static string Describe(string key, Link link, LinkStatus status)
    {
        var state = status.StateOf(link);
        var missing = state == LinkState.CardMissing ? " (card missing)" : string.Empty;

        if (link.Kind == LinkKind.Spawned)
        {
            return Same(link.To, key)
                ? $"Spawned by {Title(link.From, status)}{missing}"
                : $"Spawned {Title(link.To, status)}{(link.HandOver is { } scope ? $", handing over {scope}" : string.Empty)}{missing}";
        }

        var accepted = state switch
        {
            LinkState.Satisfied => "accepted",
            LinkState.Reported => $"reported{Verdict(link)}, not confirmed yet",
            LinkState.NoReportYet => "ran since, no report yet",
            LinkState.ReportLost => "report lost",
            _ => "not accepted yet",
        };

        return Same(link.From, key)
            ? $"{Title(link.To, status)} waits on this: {accepted}{missing}"
            : $"Waits on {Title(link.From, status)}: {accepted}{missing}";
    }

    private static string Verdict(Link link) =>
        link.LatestReport?.Verdict is { } verdict ? $" ({verdict})" : string.Empty;

    private static string Names(IReadOnlyList<string> keys, LinkStatus status)
    {
        var shown = string.Join(", ", keys.Take(NamesShown).Select(key => Title(key, status)));

        return keys.Count > NamesShown ? $"{shown} +{keys.Count - NamesShown}" : shown;
    }

    private static string Title(string key, LinkStatus status) => status.Card(key)?.Title ?? key;

    private static bool Same(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
