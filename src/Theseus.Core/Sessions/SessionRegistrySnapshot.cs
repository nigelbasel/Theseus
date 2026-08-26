namespace Theseus.Core.Sessions;

/// <summary>The live view of Claude Code sessions at one moment.</summary>
/// <param name="Live">Every entry confirmed against a running process.</param>
/// <param name="CorpseCount">
/// Entries left behind by hard-killed sessions. They are counted rather than deleted, because the
/// files belong to Claude Code and not to this application.
/// </param>
/// <param name="UnreadableCount">Entries that could not be parsed.</param>
public sealed record SessionRegistrySnapshot(
    IReadOnlyList<LiveSession> Live,
    int CorpseCount,
    int UnreadableCount)
{
    /// <summary>An empty snapshot.</summary>
    public static SessionRegistrySnapshot Empty { get; } = new([], 0, 0);

    /// <summary>
    /// Every live session carrying the given session id. More than one means the transcript has
    /// been forked, which the launch gate must refuse to compound.
    /// </summary>
    public IReadOnlyList<LiveSession> WithSessionId(string sessionId) =>
        [.. Live.Where(s => string.Equals(s.SessionId, sessionId, StringComparison.OrdinalIgnoreCase))];
}
