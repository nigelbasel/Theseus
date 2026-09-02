namespace Theseus.Core.Domain;

/// <summary>
/// Finds what two workstreams have in common that they should not.
///
/// This exists because it happened. Two sessions were pointed at one handover document, both wrote
/// to it, and the only reason nothing was lost is that one of them noticed the file changing under
/// it and asked. The board had no way to know, and neither session could see past its own card.
///
/// A shared handover document is the dangerous case: it is silent, and the loss is somebody's notes.
/// A shared Jira issue is worth saying and nothing worse - it is sometimes deliberate, and the cost
/// of the other kind is two people doing one job rather than a file being destroyed.
/// </summary>
public static class WorkstreamOverlap
{
    /// <summary>
    /// Every clash among the workstreams given, one per shared thing.
    /// </summary>
    /// <param name="workstreams">
    /// Each workstream's key, the handover document it would actually write, and the Jira issues on
    /// its card. The handover path is passed in rather than worked out here: resolving one reads the
    /// disk, and this has to stay arithmetic.
    /// </param>
    public static IReadOnlyList<WorkstreamClash> Find(
        IReadOnlyList<(string Key, string? Handoff, IReadOnlyList<string>? JiraKeys)> workstreams)
    {
        ArgumentNullException.ThrowIfNull(workstreams);

        return
        [
            .. Shared(workstreams, entry => entry.Handoff is { } handoff && handoff.Length > 0 ? [handoff] : [])
                .Select(pair => new WorkstreamClash(ClashKind.Handoff, pair.What, pair.Keys)),
            .. Shared(workstreams, entry => entry.JiraKeys ?? [])
                .Select(pair => new WorkstreamClash(ClashKind.JiraKey, pair.What, pair.Keys)),
        ];
    }

    /// <summary>What a card should say about the clashes it is caught up in, or nothing.</summary>
    public static string Describe(string workstreamKey, IReadOnlyList<WorkstreamClash> clashes)
    {
        ArgumentNullException.ThrowIfNull(clashes);

        var mine = clashes
            .Where(clash => clash.Workstreams.Contains(workstreamKey, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (mine.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(" · ", mine.Select(clash => Sentence(workstreamKey, clash)));
    }

    private static string Sentence(string workstreamKey, WorkstreamClash clash)
    {
        var others = clash.Workstreams
            .Where(key => !string.Equals(key, workstreamKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var who = others.Count == 1 ? others[0] : string.Join(", ", others);

        return clash.Kind is ClashKind.Handoff
            ? $"shares its handover notes with {who} — one will overwrite the other"
            : $"shares {clash.What} with {who}";
    }

    private static IEnumerable<(string What, IReadOnlyList<string> Keys)> Shared(
        IReadOnlyList<(string Key, string? Handoff, IReadOnlyList<string>? JiraKeys)> workstreams,
        Func<(string Key, string? Handoff, IReadOnlyList<string>? JiraKeys), IEnumerable<string>> valuesOf) =>
        workstreams
            .SelectMany(entry => valuesOf(entry)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => (Value: value.Trim(), entry.Key)))
            .GroupBy(pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            .Select(group => (
                What: group.Key,
                Keys: (IReadOnlyList<string>)[.. group
                    .Select(pair => pair.Key)
                    .Distinct(StringComparer.OrdinalIgnoreCase)]))
            .Where(group => group.Keys.Count > 1);
}
