using Theseus.Core.Settings;

namespace Theseus.Core.Resources;

/// <summary>
/// Whether the machine is holding its cores back (parked-cores, spec approved by Nigel 2026-11-19). In November 2026 a
/// laptop that had overheated kept 16 of its 20 processors parked under full load, and finding that out took most of
/// a day. Many parked processors alone is normal (the same laptop idled with 16 to 17 parked), and total busy stays
/// low in a clamp, so the sign is most processors parked while the rest are saturated, held for a while.
/// </summary>
public sealed class CoreClampWatch
{
    private readonly CoreClampSettings _settings;
    private int _run;
    private bool _standing;

    /// <summary>Creates the watch with the figures it judges by.</summary>
    public CoreClampWatch(CoreClampSettings settings) => _settings = settings ?? throw new ArgumentNullException(nameof(settings));

    /// <summary>Whether one sample shows a clamp. A sample that could not be read, or with nothing running, does not.</summary>
    public static bool ShowsClamp(CoreSample? sample, CoreClampSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return sample is { Total: > 0 } reading
               && reading.Parked < reading.Total
               && (double)reading.Parked / reading.Total >= settings.ParkedShare
               && reading.RunningBusyPercent >= settings.RunningBusyPercent;
    }

    /// <summary>Takes the next sample and says where the warning stands.</summary>
    public CoreClampState Observe(CoreSample? sample)
    {
        _run = ShowsClamp(sample, _settings) ? _run + 1 : 0;

        var was = _standing;
        _standing = _run >= Math.Max(1, _settings.HeldSamples);

        return new CoreClampState(_standing, _standing && !was, was && !_standing);
    }
}
