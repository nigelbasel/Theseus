using Theseus.Core.Domain;

namespace Theseus.Core.Stopping;

/// <summary>
/// Whose a shared handover file is: the one card its name names, by key or by title written as a key. When two names
/// appear, the owner is the card whose name contains the other's ("…-ndc-mcp-server-in-the-claude-directory" over
/// "ndc-mcp-server"); otherwise nobody owns it, and nothing is moved (handoff-names AC 1, Nigel 2026-11-19).
/// </summary>
public static class HandoffOwner
{
    /// <summary>The key of the card that owns the file, or null when that is not exactly one card.</summary>
    /// <param name="file">The shared handover file.</param>
    /// <param name="sharers">Each sharing card's key and title.</param>
    public static string? Of(string file, IReadOnlyList<(string Key, string Title)> sharers)
    {
        ArgumentNullException.ThrowIfNull(sharers);

        var name = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();

        var named = sharers
            .Select(sharer => (sharer.Key, Token: new[] { sharer.Key.ToLowerInvariant(), WorkstreamKey.FromTitle(sharer.Title) }
                .Where(token => token.Length > 0 && name.Contains(token, StringComparison.Ordinal))
                .OrderByDescending(token => token.Length)
                .FirstOrDefault()))
            .Where(match => match.Token is not null)
            .ToList();

        var owners = named
            .Where(candidate => named.All(other => candidate.Token!.Contains(other.Token!, StringComparison.Ordinal)))
            .ToList();

        return owners.Count == 1 ? owners[0].Key : null;
    }
}
