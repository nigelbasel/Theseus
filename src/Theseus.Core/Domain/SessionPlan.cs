namespace Theseus.Core.Domain;

/// <summary>
/// The on-disk shape of <c>sessions\&lt;key&gt;.json</c>, matching the existing format exactly. Its
/// writers are Claude Code sessions holding a Write tool, so the format is a contract with them and
/// changing it is not this application's decision to make alone.
/// </summary>
/// <param name="Order">Position in the morning's launch order.</param>
/// <param name="Title">The tab title.</param>
/// <param name="Color">The tab colour.</param>
/// <param name="Cwd">The working directory.</param>
/// <param name="AddDirs">Extra directories for the session's scope.</param>
/// <param name="ResumeSessionId">The session to resume. Owned by Theseus, not by the writing session.</param>
/// <param name="WrittenAt">When the plan was written, as the writer formatted it.</param>
/// <param name="Prompt">The opening prompt.</param>
public sealed record SessionPlan(
    int Order,
    string Title,
    string Color,
    string Cwd,
    IReadOnlyList<string> AddDirs,
    string? ResumeSessionId,
    string WrittenAt,
    string Prompt)
{
    /// <summary>
    /// The Jira issues this workstream is working on, as its own session recorded them.
    ///
    /// Written by the session rather than by Theseus, because an issue raised in the middle of a
    /// session exists nowhere the board can see: not in the opening prompt, which was composed before
    /// it, and not on the card, where nobody has typed it. The session is the only party that knows.
    /// Absent from plans written before this existed, and from any a session chooses not to fill in.
    /// </summary>
    public IReadOnlyList<string> JiraKeys { get; init; } = [];
}
