using Theseus.Core.Resources;
using Theseus.Core.Settings;

namespace Theseus.Tests.Resources;

/// <summary>
/// parked-cores, implement pass 3 (2026-11-19): what the re-verify found. A temporary PDH status costs one sample, not the
/// warning for the rest of the board's run; and the guards against hand-edited figures are tested so that removing them
/// fails a test.
/// </summary>
[TestFixture]
public class ParkedCoresPass3Tests
{
    [TestCase(0x800007D6u, true, Description = "PDH_CALC_NEGATIVE_DENOMINATOR, seen 4 times in 300 reads")]
    [TestCase(0x800007D5u, true, Description = "a warning-level status")]
    [TestCase(0xC0000BC6u, false, Description = "PDH_INVALID_DATA, an error-level status")]
    [TestCase(0xC0000BB8u, false, Description = "PDH_CSTATUS_NO_OBJECT")]
    [TestCase(0u, false, Description = "success is not a status to skip")]
    public void OnlyWarningLevelStatuses_AreTemporary(uint status, bool temporary)
    {
        Assert.That(PdhStatus.IsTemporary(status), Is.EqualTo(temporary));
    }

    [TestCase(0)]
    [TestCase(-2)]
    public void AHoldOfZeroOrLess_StillNeedsAClamp_IdleNeverWarns(int held)
    {
        var watch = new CoreClampWatch(new CoreClampSettings { HeldSamples = held });

        Assert.That(watch.Observe(new CoreSample(20, 17, 8, 100)).Standing, Is.False);
    }

    [Test]
    public void EveryProcessorParked_IsNoClamp_EvenIfItsBusyFigureIsHigh()
    {
        Assert.That(CoreClampWatch.ShowsClamp(new CoreSample(20, 20, 100, null), new CoreClampSettings()), Is.False);
    }
}
