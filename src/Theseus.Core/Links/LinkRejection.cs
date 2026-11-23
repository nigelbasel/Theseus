namespace Theseus.Core.Links;

/// <summary>
/// The owner's rejection of a report on a link: it never satisfies the link and leaves NEEDS YOU. One made with Reject… is
/// tied to the report it rejects, so a newer report on the same link is judged on its own; one made by Clear up
/// verifiers… names no report and covers every report on the link, since a verifier being stopped may still file one.
/// </summary>
/// <param name="AtUtc">When it was rejected.</param>
/// <param name="Reason">Why, in the owner's words.</param>
/// <param name="ReportAtUtc">When the rejected report was filed, or null for every report on the link.</param>
public sealed record LinkRejection(DateTimeOffset AtUtc, string Reason, DateTimeOffset? ReportAtUtc);
