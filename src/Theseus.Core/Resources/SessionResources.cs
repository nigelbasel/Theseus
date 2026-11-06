namespace Theseus.Core.Resources;

/// <summary>
/// What one session is using: its Claude Code process, plus every process it has started, such as
/// shells, builds, test hosts and MCP servers.
/// </summary>
/// <param name="ProcessCount">How many processes are in the session's tree.</param>
/// <param name="CpuPercent">Their share of the whole machine's processors, counted as Task Manager counts it.</param>
/// <param name="PrivateBytes">The memory they hold between them.</param>
/// <param name="IoPerSecond">Read, write and other I/O requests they make each second.</param>
/// <param name="Busiest">The processes using the most, busiest first.</param>
public sealed record SessionResources(
    int ProcessCount,
    double CpuPercent,
    long PrivateBytes,
    double IoPerSecond,
    IReadOnlyList<ProcessShare> Busiest);
