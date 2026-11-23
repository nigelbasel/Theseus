namespace Theseus.Core.Links;

/// <summary>
/// The owner's rejection of a report on a link: it never satisfies the link and leaves NEEDS YOU. It is tied to the report
/// it rejects, so a newer report on the same link is judged on its own.
/// </summary>
/// <param name="AtUtc">When it was rejected.</param>
/// <param name="Reason">Why, in the owner's words.</param>
/// <param name="ReportAtUtc">When the rejected report was filed.</param>
public sealed record LinkRejection(DateTimeOffset AtUtc, string Reason, DateTimeOffset ReportAtUtc);
