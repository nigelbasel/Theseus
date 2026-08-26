namespace Theseus.Core.Domain;

/// <summary>
/// A durable strand of work: one card on the board, one plan file, and one colour that never changes.
/// </summary>
/// <param name="Key">Kebab-case identity, equal to the plan file's basename.</param>
/// <param name="Title">The tab title and card heading.</param>
/// <param name="ColorHex">The tab colour, permanent once assigned.</param>
/// <param name="ColorIsManual">Whether the colour was chosen by hand and must never be re-derived.</param>
/// <param name="WorkingDirectory">Where its sessions start.</param>
/// <param name="AddDirectories">Extra directories added to each session's scope.</param>
/// <param name="Prompt">The opening prompt written by the last session that ran.</param>
/// <param name="BoardOrder">Position within its column.</param>
/// <param name="Column">Which column the card sits in.</param>
/// <param name="IsBlocked">A modifier on in-flight work, deliberately not a column of its own.</param>
/// <param name="ReopenTomorrow">Whether the morning ritual should start this workstream.</param>
/// <param name="IsArchived">Whether the workstream is retired and its colour returned to the pool.</param>
/// <param name="LastSessionId">The session its next launch resumes, if any.</param>
/// <param name="JiraKeys">Every Jira issue the prompt mentions. A workstream routinely spans several.</param>
/// <param name="StatusDocumentPath">The handover document the sessions maintain.</param>
/// <param name="PlanWrittenUtc">When the plan file was last written.</param>
public sealed record Workstream(
    string Key,
    string Title,
    string ColorHex,
    bool ColorIsManual,
    string WorkingDirectory,
    IReadOnlyList<string> AddDirectories,
    string Prompt,
    int BoardOrder,
    BoardColumn Column,
    bool IsBlocked,
    bool ReopenTomorrow,
    bool IsArchived,
    string? LastSessionId,
    IReadOnlyList<string> JiraKeys,
    string? StatusDocumentPath,
    DateTimeOffset PlanWrittenUtc);
