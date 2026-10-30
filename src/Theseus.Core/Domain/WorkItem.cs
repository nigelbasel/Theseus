namespace Theseus.Core.Domain;

/// <summary>
/// A Jira issue or pull request a session said its workstream works on, with the title it gave.
/// Theseus has no Jira or Azure DevOps client of its own (Nigel's rule), so a title is only ever
/// what a session, which has those tools, reported.
/// </summary>
/// <param name="Workstream">The card it belongs to.</param>
/// <param name="Kind">Jira issue or pull request.</param>
/// <param name="Id">The issue key, such as NDC-680, or the pull request number.</param>
/// <param name="Title">Its title, or null when none was given.</param>
/// <param name="Url">Where it is, or null to work it out from the board's settings.</param>
/// <param name="RecordedUtc">When it was last recorded.</param>
public sealed record WorkItem(string Workstream, WorkItemKind Kind, string Id, string? Title, string? Url, DateTimeOffset RecordedUtc);
