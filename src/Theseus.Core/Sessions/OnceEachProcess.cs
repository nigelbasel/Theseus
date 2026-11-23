using System.Collections.Concurrent;

namespace Theseus.Core.Sessions;

/// <summary>
/// Which processes have been said to be gone, so each is logged once: the board asks after every registry entry on each
/// refresh, and one exited process held open filled the log every five seconds (git-22, 2026-11-23).
/// </summary>
public sealed class OnceEachProcess
{
    private readonly ConcurrentDictionary<int, byte> _told = new();

    /// <summary>Whether this is the first time the process is said to be gone.</summary>
    public bool First(int pid) => _told.TryAdd(pid, 0);
}
