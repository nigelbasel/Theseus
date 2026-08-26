namespace Theseus.Core.Sessions;

/// <summary>One process as the operating system's process snapshot describes it.</summary>
/// <param name="ProcessId">The process.</param>
/// <param name="ParentProcessId">Its parent, which may since have exited and been reused.</param>
/// <param name="Name">The executable name, without a path or extension.</param>
public sealed record ProcessSnapshotEntry(int ProcessId, int ParentProcessId, string Name);
