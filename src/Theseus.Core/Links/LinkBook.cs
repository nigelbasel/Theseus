using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Links;

/// <summary>
/// Every link between workstreams, and the only thing that changes them. It refuses what would make
/// the board lie: a card linked to itself, the same link twice, a card that is its own ancestor, a
/// child with two parents, or a report back from a card its target never handed over to.
///
/// Sessions report through it from the MCP server's pool threads while the board changes it on the UI
/// thread, so every read and write takes the lock, and <see cref="Changed"/> is raised outside it on
/// whichever thread made the change.
/// </summary>
public sealed class LinkBook
{
    /// <summary>What <see cref="Link.SatisfiedBy"/> says for a link satisfied by confirming its report.</summary>
    public const string ConfirmedReport = "report confirmed";

    private readonly LinkStateStore _store;
    private readonly ISystemClock _clock;
    private readonly List<Link> _links;
    private readonly Lock _gate = new();

    /// <summary>Opens the book from its saved state.</summary>
    public LinkBook(LinkStateStore store, ISystemClock clock)
    {
        _store = store;
        _clock = clock;
        _links = [.. store.Load()];
    }

    /// <summary>Raised after any change is saved, on the thread that made it.</summary>
    public event EventHandler? Changed;

    /// <summary>Every link ever made, removed ones included.</summary>
    public IReadOnlyList<Link> All
    {
        get
        {
            lock (_gate)
            {
                return [.. _links];
            }
        }
    }

    /// <summary>The links that still stand.</summary>
    public IReadOnlyList<Link> Active
    {
        get
        {
            lock (_gate)
            {
                return [.. _links.Where(link => link.IsActive)];
            }
        }
    }

    /// <summary>Makes a link, unless it would contradict the ones already standing. Either way it is logged.</summary>
    public LinkChange Add(LinkKind kind, string from, string to, string? output = null)
    {
        LinkChange change;

        lock (_gate)
        {
            change = TryAdd(kind, from, to, output);
        }

        if (change.Refusal is { } refusal)
        {
            TheseusLog.Information("Link {Kind} {From} -> {To} refused: {Refusal}", kind.ToString(), from, to, refusal);
        }
        else
        {
            TheseusLog.Information("Link {Id} made: {Kind} {From} -> {To}, output {Output}", change.Link!.Id, kind.ToString(), from,
                to, change.Link.Output ?? "none");
            OnChanged();
        }

        return change;
    }

    /// <summary>
    /// Removes a link, and with a hand-over the report back that depended on it. Both are kept as
    /// tombstones, never deleted.
    /// </summary>
    public LinkChange Remove(string id)
    {
        var now = _clock.UtcNow;
        var change = Change(id, link => LinkChange.Made(link with { RemovedUtc = now }));

        if (change.Link is { Kind: LinkKind.HandsOver } handOver)
        {
            lock (_gate)
            {
                var index = _links.FindIndex(link => link.IsActive && link.Kind == LinkKind.ReportsBack
                                                     && SameKey(link.From, handOver.To) && SameKey(link.To, handOver.From));

                if (index >= 0)
                {
                    TheseusLog.Information("Link {Id} removed with the hand-over it reported back on", _links[index].Id);
                    _links[index] = _links[index] with { RemovedUtc = now };
                    Save();
                }
            }
        }

        return Logged(id, "removed", string.Empty, change);
    }

    /// <summary>Accepts a link's document by hand, for the reason Nigel gives.</summary>
    public LinkChange Satisfy(string id, string reason) =>
        Logged(id, "satisfied", $" by hand: {reason}", Change(id, link =>
        {
            if (link.Output is null)
            {
                return LinkChange.Refused("That link names no document, so there is nothing to accept.");
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return LinkChange.Refused("Say why it is satisfied; the reason is kept with the link.");
            }

            return LinkChange.Made(link with
            {
                SatisfiedUtc = _clock.UtcNow,
                SatisfiedBy = $"by hand: {reason.Trim()}",
            });
        }));

    /// <summary>
    /// Confirms the report on a link, which satisfies it. Only Nigel does this, from the board:
    /// no session tool reaches it.
    /// </summary>
    public LinkChange Confirm(string id) =>
        Logged(id, "satisfied", " by confirming its report", Change(id, link =>
        {
            if (link.LatestReport is null)
            {
                return LinkChange.Refused("There is no report on that link to confirm.");
            }

            if (link.SatisfiedUtc is not null)
            {
                return LinkChange.Refused("That link is already satisfied.");
            }

            return LinkChange.Made(link with { SatisfiedUtc = _clock.UtcNow, SatisfiedBy = ConfirmedReport });
        }));

    /// <summary>
    /// Records a report on one or more links: a hand-over from the parent, or a report back from the
    /// child. It replaces each link's latest report and changes nothing else: the link shows Reported
    /// until Nigel confirms it.
    /// </summary>
    /// <param name="from">The reporting workstream, which must be the writing end of every link named.</param>
    /// <param name="sessionId">The session sending it, kept as evidence.</param>
    /// <param name="linkIds">The links reported on. May be empty when exactly one link is eligible.</param>
    /// <param name="document">The output document. It must be the path recorded on each link.</param>
    /// <param name="summary">What was done, at most <see cref="LinkReport.MaxSummaryLength"/> characters.</param>
    /// <param name="verdict">One of <see cref="ReportVerdicts.All"/>, or null.</param>
    public ReportOutcome Report(string from, string sessionId, IReadOnlyList<string> linkIds, string document,
        string summary, string? verdict)
    {
        ReportOutcome outcome;

        lock (_gate)
        {
            outcome = TryReport(from, sessionId, linkIds, document, summary, verdict);
        }

        if (outcome.Refusal is { } refusal)
        {
            TheseusLog.Information("Report from {From} (session {SessionId}) refused: {Refusal}", from, sessionId, refusal);
        }
        else
        {
            TheseusLog.Information("Report from {From} (session {SessionId}) recorded on {Links}, verdict {Verdict}", from,
                sessionId, string.Join(", ", outcome.Links.Select(link => link.Id)), verdict ?? "none");
            OnChanged();
        }

        return outcome;
    }

    /// <summary>Removes every link touching a card that has been deleted.</summary>
    public void RemoveAllFor(string key)
    {
        var now = _clock.UtcNow;
        var removed = 0;

        lock (_gate)
        {
            for (var index = 0; index < _links.Count; index++)
            {
                if (_links[index].IsActive && _links[index].Touches(key))
                {
                    _links[index] = _links[index] with { RemovedUtc = now };
                    removed++;
                }
            }

            if (removed > 0)
            {
                Save();
            }
        }

        if (removed > 0)
        {
            TheseusLog.Information("Removed {Count} link(s) of deleted card {Key}", removed, key);
            OnChanged();
        }
    }

    /// <summary>
    /// Whether any link, removed or not, names this key. A deleted card's key stays taken, so a new
    /// card with the same title gets a fresh key and inherits nothing.
    /// </summary>
    public bool IsKeyTaken(string key)
    {
        lock (_gate)
        {
            return _links.Any(link => link.Touches(key));
        }
    }

    private LinkChange Logged(string id, string what, string detail, LinkChange change)
    {
        if (change.Refusal is { } refusal)
        {
            TheseusLog.Information("Link {Id} not {What}: {Refusal}", id, what, refusal);
        }
        else
        {
            TheseusLog.Information("Link {Id} {What}{Detail}", id, what, detail);
            OnChanged();
        }

        return change;
    }

    private LinkChange TryAdd(LinkKind kind, string from, string to, string? output)
    {
        if (SameKey(from, to))
        {
            return LinkChange.Refused("A card cannot be linked to itself.");
        }

        var active = _links.Where(link => link.IsActive).ToList();

        if (active.Any(link => link.Kind == kind && SameKey(link.From, from) && SameKey(link.To, to)))
        {
            return LinkChange.Refused("Those two cards are already linked that way.");
        }

        if (kind == LinkKind.HandsOver)
        {
            if (active.FirstOrDefault(link => link.Kind == LinkKind.HandsOver && SameKey(link.To, to)) is { } parent)
            {
                return LinkChange.Refused($"{parent.From} already hands over to {to}; a card takes over from one card only.");
            }

            if (Reaches(active, to, from))
            {
                return LinkChange.Refused($"That would make {to} its own ancestor.");
            }
        }

        if (kind == LinkKind.ReportsBack
            && !active.Any(link => link.Kind == LinkKind.HandsOver && SameKey(link.From, to) && SameKey(link.To, from)))
        {
            return LinkChange.Refused($"{to} does not hand over to {from}, so {from} has nothing to report back to it.");
        }

        var link = new Link(NewId(), kind, from, to, _clock.UtcNow, Link.MadeOnTheBoard, Output: Blank(output));

        _links.Add(link);
        Save();

        return LinkChange.Made(link);
    }

    private LinkChange Change(string id, Func<Link, LinkChange> change)
    {
        lock (_gate)
        {
            var index = _links.FindIndex(link => link.Id == id && link.IsActive);

            if (index < 0)
            {
                return LinkChange.Refused("That link no longer exists.");
            }

            var result = change(_links[index]);

            if (result.Link is { } changed)
            {
                _links[index] = changed;
                Save();
            }

            return result;
        }
    }

    private ReportOutcome TryReport(string from, string sessionId, IReadOnlyList<string> linkIds, string document,
        string summary, string? verdict)
    {
        if (string.IsNullOrWhiteSpace(document))
        {
            return ReportOutcome.Refused("document is required: the output document named on the link.");
        }

        if (string.IsNullOrWhiteSpace(summary))
        {
            return ReportOutcome.Refused("summary is required: what was done, what was found, what needs attention.");
        }

        if (summary.Length > LinkReport.MaxSummaryLength)
        {
            return ReportOutcome.Refused(
                $"summary is {summary.Length} characters; the most is {LinkReport.MaxSummaryLength}. The document is the authority, so keep the summary to a pointer.");
        }

        var word = ReportVerdicts.Parse(verdict);

        if (!string.IsNullOrWhiteSpace(verdict) && word is null)
        {
            return ReportOutcome.Refused($"verdict must be one of {string.Join(", ", ReportVerdicts.All)}, or left out.");
        }

        var chosen = new List<Link>();

        if (linkIds.Count == 0)
        {
            var eligible = _links.Where(link => link.IsActive && link.Output is not null && SameKey(link.From, from)).ToList();

            if (eligible.Count == 0)
            {
                return ReportOutcome.Refused("No link asks this workstream for a document, so there is nothing to report on.");
            }

            if (eligible.Count > 1)
            {
                return ReportOutcome.Refused(
                    "More than one link asks this workstream for a document, so say which with links: "
                    + string.Join("; ", eligible.Select(link => $"{link.Id} (to {link.To}, output {link.Output ?? "none"})")) + ".");
            }

            chosen.Add(eligible[0]);
        }
        else
        {
            foreach (var id in linkIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (_links.FirstOrDefault(link => link.IsActive && string.Equals(link.Id, id, StringComparison.OrdinalIgnoreCase)) is not { } link)
                {
                    return ReportOutcome.Refused($"There is no link {id}.");
                }

                if (!SameKey(link.From, from))
                {
                    return ReportOutcome.Refused($"This workstream does not write the document for {id}, so it cannot report on it.");
                }

                chosen.Add(link);
            }
        }

        foreach (var link in chosen)
        {
            if (link.Output is null)
            {
                return ReportOutcome.Refused($"Link {link.Id} names no output document. Nigel sets it on the board.");
            }

            if (!SamePath(link.Output, document))
            {
                return ReportOutcome.Refused(
                    $"document must be the output recorded on link {link.Id}: {link.Output}. Changing it is a board edit.");
            }
        }

        var report = new LinkReport(_clock.UtcNow, sessionId, document.Trim(), summary.Trim(), word);
        var reported = new List<Link>();

        foreach (var link in chosen)
        {
            var index = _links.IndexOf(link);

            _links[index] = link with { LatestReport = report };
            reported.Add(_links[index]);
        }

        Save();

        return ReportOutcome.Recorded(reported);
    }

    private static bool Reaches(IReadOnlyList<Link> links, string start, string target)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>([start]);

        while (pending.TryPop(out var key))
        {
            if (SameKey(key, target))
            {
                return true;
            }

            if (!seen.Add(key))
            {
                continue;
            }

            foreach (var link in links.Where(link => link.Kind == LinkKind.HandsOver && SameKey(link.From, key)))
            {
                pending.Push(link.To);
            }
        }

        return false;
    }

    private static bool SameKey(string left, string right) =>
        string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool SamePath(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left.Trim()), Path.GetFullPath(right.Trim()), StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string NewId() => $"lk-{Guid.NewGuid():N}"[..11];

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private void Save() => _store.Save(_links);
}
