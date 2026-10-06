namespace Theseus.Core.Locks;

/// <summary>Tells the lock book whether the things holding locks are still there.</summary>
public interface ILockLiveness
{
    /// <summary>The process's start time as a file time, or null when no such process is running.</summary>
    string? StartOf(int processId);

    /// <summary>Whether a Claude session with this id has a live process.</summary>
    bool IsSessionAlive(string sessionId);
}
