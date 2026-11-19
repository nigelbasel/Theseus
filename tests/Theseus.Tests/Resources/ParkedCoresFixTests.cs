using Theseus.Core.Resources;
using Theseus.Core.Settings;

namespace Theseus.Tests.Resources;

/// <summary>
/// parked-cores, implement pass 2 (2026-11-19): what verify part 1 found. The tooltip names the setting that governs
/// core parking, and the rule holds up to figures a hand-edited settings file might give it.
/// </summary>
[TestFixture]
public class ParkedCoresFixTests
{
    private static readonly CoreSample Clamp = new(20, 16, RunningBusyPercent: 98, ClockPercent: 120);

    [Test]
    public void TheTooltip_NamesThePowerPlansMinimumCoresSetting_WhichGovernsParking()
    {
        var tooltip = CoreClampText.Tooltip(Clamp);

        Assert.That(tooltip, Does.Contain("the power plan's minimum cores setting (core parking)"));
        Assert.That(tooltip, Does.Not.Contain("processor state"), "minimum processor state is a different setting, about clock speed");
    }

    [TestCase(0)]
    [TestCase(-2)]
    public void AHoldOfZeroOrLess_ActsAsOne(int held)
    {
        var watch = new CoreClampWatch(new CoreClampSettings { HeldSamples = held });

        Assert.That(watch.Observe(Clamp).Standing, Is.True);
    }

    [Test]
    public void AParkedShareAboveOne_NeverWarns()
    {
        var watch = new CoreClampWatch(new CoreClampSettings { ParkedShare = 1.5, HeldSamples = 1 });

        Assert.That(watch.Observe(new CoreSample(20, 19, 100, null)).Standing, Is.False);
    }

    [Test]
    public void EveryProcessorParked_IsNoClamp_ThereIsNothingRunningToBeSaturated()
    {
        Assert.That(CoreClampWatch.ShowsClamp(new CoreSample(20, 20, 0, null), new CoreClampSettings()), Is.False);
    }
}
