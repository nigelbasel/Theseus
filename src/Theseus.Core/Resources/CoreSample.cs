namespace Theseus.Core.Resources;

/// <summary>One reading of the machine's logical processors: how many are parked, and how busy the rest are.</summary>
/// <param name="Total">Logical processors.</param>
/// <param name="Parked">How many of them Windows has parked.</param>
/// <param name="RunningBusyPercent">The average busy percentage of the processors not parked.</param>
/// <param name="ClockPercent">The processors' clock against base, or null when it could not be read.</param>
public sealed record CoreSample(int Total, int Parked, double RunningBusyPercent, double? ClockPercent);
