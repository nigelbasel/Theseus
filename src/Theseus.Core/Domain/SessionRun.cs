namespace Theseus.Core.Domain;

/// <summary>
/// One launch attempt, appended and never rewritten.
/// </summary>
/// <param name="Id">A surrogate key, because a Claude session identifier is not unique across working directories.</param>
/// <param name="ClaudeSessionId">The identifier the session ran under.</param>
/// <param name="WorkstreamKey">The workstream this run belongs to.</param>
/// <param name="WorkingDirectory">
/// A snapshot, not a reference: resuming is scoped to a working directory, and the same identifier
/// can legitimately exist under two of them.
/// </param>
/// <param name="TranscriptPath">Where the transcript landed, once it is known.</param>
/// <param name="LaunchMode">How the run was started.</param>
/// <param name="OpeningPrompt">The prompt the session opened with.</param>
/// <param name="TabTitle">The title given to the tab.</param>
/// <param name="ColorHex">The colour the tab was given.</param>
/// <param name="StartedUtc">When the launch was issued.</param>
/// <param name="EndedUtc">
/// When the session ended, where that was observed. Null means either that it has not ended or that
/// it ended unwatched; <see cref="SessionRun.HasEnded"/> is what separates those.
/// </param>
/// <param name="ShimProcessId">The launcher process that owns the tab.</param>
/// <param name="ClaudeProcessId">The Claude Code process.</param>
/// <param name="ExitCode">Claude Code's exit code, when one was observed.</param>
/// <param name="ExitReason">Why the run ended.</param>
public sealed record SessionRun(
    Guid Id,
    string ClaudeSessionId,
    string WorkstreamKey,
    string WorkingDirectory,
    string? TranscriptPath,
    SessionLaunchMode LaunchMode,
    string OpeningPrompt,
    string TabTitle,
    string ColorHex,
    DateTimeOffset StartedUtc,
    DateTimeOffset? EndedUtc,
    int? ShimProcessId,
    int? ClaudeProcessId,
    int? ExitCode,
    SessionExitReason ExitReason)
{
    /// <summary>
    /// Whether this run is over. Separate from <see cref="EndedUtc"/> because a run can be known to
    /// have ended without its moment being known, and a card that reads a missing moment as "still
    /// running" reports work in progress that finished hours ago.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool HasEnded => ExitReason is not SessionExitReason.Unknown;

    /// <summary>
    /// How long the run lasted, where both ends were observed. Null whenever either was not, so an
    /// unmeasured run is absent from a total rather than counted as nothing.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public TimeSpan? Duration => EndedUtc is { } ended ? ended - StartedUtc : null;
}
