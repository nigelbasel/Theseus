namespace Theseus.Core.Links;

/// <summary>What became of a report: the links it now stands on, or why nothing was recorded.</summary>
/// <param name="Links">The links carrying the report, when it was recorded.</param>
/// <param name="Refusal">One sentence saying why nothing was recorded, when it was refused.</param>
public sealed record ReportOutcome(IReadOnlyList<Link> Links, string? Refusal)
{
    /// <summary>A report that was recorded.</summary>
    public static ReportOutcome Recorded(IReadOnlyList<Link> links) => new(links, null);

    /// <summary>A report that was refused.</summary>
    public static ReportOutcome Refused(string refusal) => new([], refusal);
}
