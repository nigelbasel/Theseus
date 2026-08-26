namespace Theseus.Core.Sessions;

/// <summary>A registry entry that has been confirmed against a running process.</summary>
/// <param name="Pid">The process id.</param>
/// <param name="SessionId">The Claude session identifier.</param>
/// <param name="Cwd">The working directory the session started in.</param>
/// <param name="Name">The session's display name.</param>
/// <param name="Status">The raw activity string from the registry.</param>
/// <param name="Entrypoint">How the session was started.</param>
/// <param name="StartedUtc">When the session started.</param>
public sealed record LiveSession(
    int Pid,
    string SessionId,
    string? Cwd,
    string? Name,
    string? Status,
    string? Entrypoint,
    DateTimeOffset StartedUtc)
{
    /// <summary>
    /// Whether this session is a real terminal session a workstream could adopt, as opposed to a
    /// scripted non-interactive run. Entries without an entrypoint are not adoptable.
    /// </summary>
    public bool IsAdoptable => string.Equals(Entrypoint, "cli", StringComparison.Ordinal);
}
