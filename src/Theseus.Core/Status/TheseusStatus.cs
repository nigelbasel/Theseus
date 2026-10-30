namespace Theseus.Core.Status;

/// <summary>
/// What <c>status.json</c> says about Theseus: which process is running it, since when, and when it
/// closed. A file and not an event, because Theseus can tell no session anything once it has stopped,
/// least of all when it crashed; sessions' hooks and the lock script read it instead.
/// </summary>
/// <param name="ProcessId">The Theseus process that wrote it.</param>
/// <param name="StartedUtc">When that process started serving.</param>
/// <param name="StoppedUtc">When it closed, or null while it runs or if it never closed cleanly.</param>
/// <param name="Version">Its build.</param>
public sealed record TheseusStatus(int ProcessId, DateTimeOffset StartedUtc, DateTimeOffset? StoppedUtc, string Version);
