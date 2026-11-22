namespace Theseus.Core.Projects;

/// <summary>
/// A session's report that the owner accepted its spec in its tab (1.1e AC 28): nothing is accepted by it. It waits on
/// the board for the owner to confirm, which records the acceptance as said in the card's tab and confirmed there.
/// </summary>
/// <param name="Project">The project's name.</param>
/// <param name="Slug">The workstream's slug.</param>
/// <param name="ContentHash">The version the owner is reported to have accepted: the spec's text as it was then.</param>
/// <param name="CardKey">The card whose session reported it.</param>
/// <param name="CardTitle">Its title, as the box names it.</param>
/// <param name="OwnerWords">The owner's words, as the session quoted them.</param>
/// <param name="ReportedUtc">When it was reported.</param>
public sealed record ReportedAcceptance(
    string Project,
    string Slug,
    string ContentHash,
    string CardKey,
    string CardTitle,
    string OwnerWords,
    DateTimeOffset ReportedUtc);
