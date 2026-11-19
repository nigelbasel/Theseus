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
}
