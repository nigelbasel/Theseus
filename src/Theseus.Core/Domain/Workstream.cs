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
/// <param name="Column">The lane the card sits in, by name.</param>
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
    string Column,
    bool IsBlocked,
    bool ReopenTomorrow,
    bool IsArchived,
    string? LastSessionId,
    IReadOnlyList<string> JiraKeys,
    string? StatusDocumentPath,
    DateTimeOffset PlanWrittenUtc)
{
    /// <summary>The row of the board this card sits in: the kind of work, not how far along it is.</summary>
    public string? Area { get; init; }

    /// <summary>Whether the work is paused: set aside on purpose, keeping its stage. A flag like blocked.</summary>
    public bool IsPaused { get; init; }

    /// <summary>
    /// The card whose session paused it, through pause_workstream, or null when the owner paused it or it is not paused
    /// (1.1e AC 14): a project's Start does not start it while this stands.
    /// </summary>
    public string? PausedBy { get; init; }

    /// <summary>Why that session paused it, or null.</summary>
    public string? PauseReason { get; init; }

    /// <summary>The keys taken off this card by hand, which stay off until they are added again.</summary>
    public IReadOnlyList<string> RemovedJiraKeys { get; init; } = [];

    /// <summary>
    /// The terminal window this workstream's sessions start in, when it is not the one its lane
    /// implies. Empty means the lane decides, or the single window does when lanes are not grouping.
    /// </summary>
    public string? WindowGroup { get; init; }

    /// <summary>
    /// What this workstream's sessions are told every time they start, after everything the board
    /// says. The opening prompt is used once, and the continuation is the same for every workstream,
    /// so without this there is nowhere to tell one workstream's sessions something every time.
    /// </summary>
    public string? EveryStartPrompt { get; init; }

    /// <summary>What this card's sessions are for: none, or verifier (verifier-card, 2026-11-19).</summary>
    public WorkstreamRole Role { get; init; }

    /// <summary>The owner's tags on the card, from its sidecar (card-search-and-tags item 9).</summary>
    public IReadOnlyList<string> Tags { get; init; } = [];

    /// <summary>The card's own run mode, tab or headless; null to take its project's default, then its role's (Nigel, 2026-11-24).</summary>
    public Running.RunMode? RunMode { get; init; }

    /// <summary>The circuit breaker that tripped on this card, kept until the owner resets it; null when none has.</summary>
    public Breakers.BreakerTrip? Breaker { get; init; }

    /// <summary>When the owner last reset the card's breaker: its breakers count only the runs since.</summary>
    public DateTimeOffset? BreakerResetUtc { get; init; }

    /// <summary>
    /// The project this card is in, or null for ad hoc work: never stored, but set each time the projects are read,
    /// from the project file whose workstreams table names the card (1.1c AC 1).
    /// </summary>
    public string? Project { get; init; }

    /// <summary>Its current slug in that project: the last row naming it (1.1c AC 2).</summary>
    public string? Slug { get; init; }

    /// <summary>
    /// An instruction for this one start only, such as Restart for more work's (1.1e AC 12, 15): the session's opening
    /// prompt ends with it, after the standing and every-start prompts. Never stored, so a later start does not repeat it.
    /// </summary>
    public string? StartInstruction { get; init; }
}
