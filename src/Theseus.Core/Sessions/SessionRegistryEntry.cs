namespace Theseus.Core.Sessions;

/// <summary>
/// One record from Claude Code's live session registry, as written to
/// <c>%USERPROFILE%\.claude\sessions\{pid}.json</c>. The file is created when a session starts and
/// deleted on a clean exit, but is left behind after a hard kill or a reboot.
/// </summary>
public sealed record SessionRegistryEntry
{
    /// <summary>The operating system process id of the Claude Code process.</summary>
    public int Pid { get; init; }

    /// <summary>The Claude session identifier. Not unique: several live entries may share one.</summary>
    public string? SessionId { get; init; }

    /// <summary>The working directory the session was started in.</summary>
    public string? Cwd { get; init; }

    /// <summary>
    /// The process creation time as a file time, used to tell a live process from a reused pid.
    /// </summary>
    public string? ProcStart { get; init; }

    /// <summary>The Claude Code version that wrote the entry.</summary>
    public string? Version { get; init; }

    /// <summary>How the session presents itself, for example <c>interactive</c>.</summary>
    public string? Kind { get; init; }

    /// <summary>
    /// How the session was started. <c>cli</c> is a real terminal session; <c>sdk-cli</c> is a
    /// non-interactive run. Older builds omit this.
    /// </summary>
    public string? Entrypoint { get; init; }

    /// <summary>The session's display name.</summary>
    public string? Name { get; init; }

    /// <summary>
    /// The session's activity, for example <c>busy</c> or <c>idle</c>. Edge-triggered, so it may be
    /// far older than the session's real last activity.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>When the session started, in milliseconds since the Unix epoch.</summary>
    public long StartedAt { get; init; }

    /// <summary>
    /// When the entry was last written, in milliseconds since the Unix epoch. A session rewrites its
    /// entry as it works, so this is the last moment it was known to be alive - which is the only
    /// evidence available about a session that was killed without writing anything further.
    /// </summary>
    public long UpdatedAt { get; init; }

    /// <summary>
    /// When <see cref="Status"/> last changed, in milliseconds since the Unix epoch. A status change
    /// is dated by the session that made it rather than by the moment a watcher noticed, which
    /// matters because the watcher is debounced and only runs while this application does.
    /// </summary>
    public long StatusUpdatedAt { get; init; }
}
