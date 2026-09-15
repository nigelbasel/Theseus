namespace Theseus.Core.Domain;

/// <summary>
/// One entry in <c>board.json</c>: everything about a workstream that the plan format has no room
/// for. Keyed to the plan file, so a plan can be hand-edited or hand-written without losing it.
/// </summary>
/// <param name="Key">The workstream's key, matching its plan file's basename.</param>
/// <param name="Column">The lane the card sits in, by name.</param>
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
    string Column,
    bool IsBlocked,
    bool IsArchived,
    bool ColorIsManual,
    bool ReopenTomorrow,
    IReadOnlyList<string> JiraKeys,
    string? StatusDocumentPath,
    string? LastSessionId,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc)
{
    /// <summary>
    /// The keys taken off this card by hand. Kept so that removing one means something lasting: the
    /// prompt and the session both go on naming keys every time the board is read, and without a
    /// record of what was dismissed there would be no way to be rid of one.
    /// </summary>
    public IReadOnlyList<string> RemovedJiraKeys { get; init; } = [];

    /// <summary>The row of the board this card sits in: the kind of work, not how far along it is.</summary>
    public string? Area { get; init; }

    /// <summary>The terminal window this workstream's sessions start in, if not the one its area implies.</summary>
    public string? WindowGroup { get; init; }

    /// <summary>
    /// What this workstream's sessions are told on every start. Kept here and not in the plan file,
    /// because sessions rewrite their plan files and would drop a field they do not know about.
    /// </summary>
    public string? EveryStartPrompt { get; init; }
}
