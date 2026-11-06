namespace Theseus.Core.Resources;

/// <summary>
/// One process's running totals at one instant, as the kernel keeps them. These are totals, not
/// rates: a rate needs two readings, and the sampler supplies the second.
/// </summary>
/// <param name="ProcessId">The process.</param>
/// <param name="ParentProcessId">The process that started it. That one may since have exited and its id been reused.</param>
/// <param name="Name">The executable's name, without its extension.</param>
/// <param name="CreateTime">When it started, as a UTC file time. This tells it apart from a later process given the same id.</param>
/// <param name="CpuTicks">Processor time it has used, user and kernel, in 100-nanosecond ticks.</param>
/// <param name="PrivateBytes">Memory it holds that no other process shares. This is the memory figure Task Manager shows.</param>
/// <param name="IoOperations">Reads, writes and other I/O requests it has made, to files and devices together.</param>
public sealed record ProcessCounters(
    int ProcessId,
    int ParentProcessId,
    string Name,
    long CreateTime,
    long CpuTicks,
    long PrivateBytes,
    long IoOperations);
