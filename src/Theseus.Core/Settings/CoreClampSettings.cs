namespace Theseus.Core.Settings;

/// <summary>
/// The figures behind the cores-held-back warning (parked-cores, spec approved by Nigel 2026-11-19). Provisional: they
/// come from one machine's recorded samples (`work/parked-cores/research.md`) and are kept in the settings file, not
/// the settings window, so they can be revisited as more is seen.
/// </summary>
public sealed record CoreClampSettings
{
    /// <summary>The share of logical processors that must be parked, from 0 to 1.</summary>
    public double ParkedShare { get; init; } = 0.5;

    /// <summary>How busy, on average, the processors not parked must be, in percent.</summary>
    public double RunningBusyPercent { get; init; } = 90;

    /// <summary>How many samples in a row must show it before the warning shows (5 seconds each).</summary>
    public int HeldSamples { get; init; } = 3;
}
