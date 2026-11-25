using Theseus.Core.Domain;

namespace Theseus.Core.Stopping;

/// <summary>
/// Whose a shared handover file is: the one card it is named exactly after, <c>HANDOFF-&lt;key&gt;.md</c> or
/// <c>HANDOFF-&lt;title as a key&gt;.md</c>. Exact, not "contains": a short key such as <c>api</c> appears inside many
/// names, and a guessed owner moves somebody's notes (handoff-names AC 1).
/// </summary>
public static class HandoffOwner
{
    private static readonly string[] Prefixes = ["HANDOFF-", "HANDOVER-", "SESSION-STATUS-"];

    /// <summary>The key of the card that owns the file, or null when that is not exactly one card.</summary>
    /// <param name="file">The shared handover file.</param>
    /// <param name="sharers">Each sharing card's key and title.</param>
    public static string? Of(string file, IReadOnlyList<(string Key, string Title)> sharers)
    {
        ArgumentNullException.ThrowIfNull(sharers);

        var stem = Path.GetFileNameWithoutExtension(file);
        var prefix = Prefixes.FirstOrDefault(candidate => stem.StartsWith(candidate, StringComparison.OrdinalIgnoreCase));

        if (prefix is null)
        {
            return null;
        }

        var name = stem[prefix.Length..];

        var owners = sharers
            .Where(sharer => string.Equals(sharer.Key, name, StringComparison.OrdinalIgnoreCase)
                             || (!string.IsNullOrWhiteSpace(sharer.Title)
                                 && string.Equals(WorkstreamKey.FromTitle(sharer.Title), name, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return owners.Count == 1 ? owners[0].Key : null;
    }
}
