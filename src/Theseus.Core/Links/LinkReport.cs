namespace Theseus.Core.Links;

/// <summary>
/// What an upstream session said when its output was ready. A report is a claim, not a fact: it never
/// satisfies the link by itself, and the link waits for Nigel's Confirm.
/// </summary>
/// <param name="AtUtc">When it arrived.</param>
/// <param name="SessionId">The session that sent it. Session ids are not secret, so this is evidence, not proof.</param>
/// <param name="Document">The output document, which is the authority; the summary only points at it.</param>
/// <param name="Summary">What the session said it did, at most <see cref="MaxSummaryLength"/> characters.</param>
/// <param name="Verdict">One of <see cref="ReportVerdicts.All"/>, or null.</param>
public sealed record LinkReport(
    DateTimeOffset AtUtc,
    string SessionId,
    string Document,
    string Summary,
    string? Verdict)
{
    /// <summary>The longest summary accepted.</summary>
    public const int MaxSummaryLength = 2000;
}
