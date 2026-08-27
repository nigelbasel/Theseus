namespace Theseus.Core.Sessions;

/// <summary>A registry entry that has been confirmed against a running process.</summary>
/// <param name="Pid">The process id.</param>
/// <param name="SessionId">The Claude session identifier.</param>
/// <param name="Cwd">The working directory the session started in.</param>
/// <param name="Name">The session's display name.</param>
/// <param name="Status">The raw activity string from the registry.</param>
/// <param name="Entrypoint">How the session was started.</param>
/// <param name="StartedUtc">When the session started.</param>
/// <param name="LastSeenUtc">
/// When the entry was last written. A session rewrites it as it works, so this is the last moment it
/// was known to be alive - the only evidence there is about one that was killed without a word.
/// </param>
/// <param name="StatusSinceUtc">
/// When the reported status last changed, as dated by the session rather than by the moment a
/// watcher noticed. The watcher is debounced and only runs while this application does, so its own
/// clock is the worse of the two.
/// </param>
public sealed record LiveSession(
    int Pid,
    string SessionId,
    string? Cwd,
    string? Name,
    string? Status,
    string? Entrypoint,
    DateTimeOffset StartedUtc,
    DateTimeOffset? LastSeenUtc = null,
    DateTimeOffset? StatusSinceUtc = null)
{
    /// <summary>
    /// Whether this session is a real terminal session a workstream could adopt, as opposed to a
    /// scripted non-interactive run. Entries without an entrypoint are not adoptable.
    /// </summary>
    public bool IsAdoptable => string.Equals(Entrypoint, "cli", StringComparison.Ordinal);
}
