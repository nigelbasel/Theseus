namespace Theseus.Core.Resources;

/// <summary>Every process on the machine, and the processors' own totals, taken at one instant.</summary>
/// <param name="CapturedUtc">When it was taken.</param>
/// <param name="Processes">Every process.</param>
/// <param name="IdleTicks">Time all processors have spent idle since start-up, in 100-nanosecond ticks.</param>
/// <param name="BusyAndIdleTicks">Total time all processors have run, idle time included.</param>
public sealed record ProcessTable(
    DateTimeOffset CapturedUtc,
    IReadOnlyList<ProcessCounters> Processes,
    long IdleTicks,
    long BusyAndIdleTicks)
{
    /// <summary>A table with nothing in it, for when the operating system refuses.</summary>
    public static ProcessTable Empty(DateTimeOffset at) => new(at, [], 0, 0);
}
