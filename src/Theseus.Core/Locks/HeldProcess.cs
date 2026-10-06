namespace Theseus.Core.Locks;

/// <summary>A test host seen running under a lock's holder, recorded so it can be found again exactly.</summary>
/// <param name="ProcessId">Its Windows process id.</param>
/// <param name="Start">Its start time as a file time, so a reused id is never mistaken for it.</param>
/// <param name="Name">Its image name, for the log.</param>
public sealed record HeldProcess(int ProcessId, string Start, string Name);
