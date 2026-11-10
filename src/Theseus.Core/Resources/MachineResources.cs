namespace Theseus.Core.Resources;

/// <summary>What the whole machine is doing, including the load that belongs to no session.</summary>
/// <param name="CpuPercent">How busy the processors are.</param>
/// <param name="DefenderPercent">The share used by Microsoft Defender's scanning engine and its Defender for Endpoint sensor.</param>
/// <param name="DockerPercent">
/// The share used by the WSL virtual machine. Docker Desktop's containers run inside it, so a test
/// run's SQL Server shows up here. It belongs to no session's process tree.
/// </param>
/// <param name="SessionsPercent">The share used by all sessions' process trees together.</param>
public sealed record MachineResources(
    double CpuPercent,
    double DefenderPercent,
    double DockerPercent,
    double SessionsPercent);
