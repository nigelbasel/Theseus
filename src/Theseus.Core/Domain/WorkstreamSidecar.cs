namespace Theseus.Core.Domain;

/// <summary>
/// One entry in <c>board.json</c>: everything about a workstream that the plan format has no room
/// for. Keyed to the plan file, so a plan can be hand-edited or hand-written without losing it.
/// </summary>
/// <param name="Key">The workstream's key, matching its plan file's basename.</param>
/// <param name="Column">Which column the card sits in.</param>
/// <param name="IsBlocked">Whether the work is blocked.</param>
/// <param name="IsArchived">Whether the workstream is retired.</param>
/// <param name="ColorIsManual">Whether the colour was chosen by hand.</param>
/// <param name="ReopenTomorrow">Whether the morning ritual should start it.</param>
/// <param name="JiraKeys">Every Jira issue the workstream touches.</param>
/// <param name="StatusDocumentPath">The handover document.</param>
/// <param name="LastSessionId">The session the next launch resumes.</param>
/// <param name="CreatedUtc">When the sidecar was first written.</param>
/// <param name="UpdatedUtc">When the sidecar last changed.</param>
public sealed record WorkstreamSidecar(
    string Key,
    BoardColumn Column,
    bool IsBlocked,
    bool IsArchived,
    bool ColorIsManual,
    bool ReopenTomorrow,
    IReadOnlyList<string> JiraKeys,
    string? StatusDocumentPath,
    string? LastSessionId,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);
