using Theseus.Core.Domain;

namespace Theseus.Core.Links;

/// <summary>
/// When confirming a report also stops the card that wrote it: only a verifier's report, whose confirmation ends its
/// job. The only stop that is not a press, and it follows the owner's own confirm (verifier-card AC 13; Nigel's idea,
/// decision D5, 2026-11-19).
/// </summary>
public static class VerifierEnding
{
    /// <summary>Whether confirming a report on this link stops the card it comes from.</summary>
    public static bool StopsOnConfirm(LinkKind kind, WorkstreamRole fromRole) =>
        kind == LinkKind.ReportsBack && fromRole == WorkstreamRole.Verifier;

    /// <summary>
    /// Whether a verifier's job is over: its report back has been accepted, which is also what stops it. Its card then
    /// leaves its subject's row in the project view (project-view AC 10). Removing the link afterwards does not bring
    /// it back: what was accepted stays accepted (PR 2 review, item 6).
    /// </summary>
    public static bool HasEnded(IEnumerable<Link> links, string verifierKey)
    {
        ArgumentNullException.ThrowIfNull(links);

        return links.Any(link => link.Kind == LinkKind.ReportsBack && link.SatisfiedUtc is not null
                                 && string.Equals(link.From, verifierKey, StringComparison.OrdinalIgnoreCase));
    }
}
