using Theseus.Core.Resources;

namespace Theseus.Tests.Resources;

/// <summary>What the status bar and the log say about cores held back (parked-cores spec AC 6 to 8).</summary>
[TestFixture]
public class CoreClampTextTests
{
    private static readonly CoreSample Clamp = new(20, 16, RunningBusyPercent: 98.4, ClockPercent: 121.6);

    [Test]
    public void TheStatusBar_SaysHowManyOfHowManyAreParked()
    {
        Assert.That(CoreClampText.Status(Clamp), Is.EqualTo("Cores held back: 16 of 20 parked under load"));
    }

    [Test]
    public void TheTooltip_SaysWhatItMeansWhatToCheckAndTheFigures()
    {
        var tooltip = CoreClampText.Tooltip(Clamp);

        Assert.That(tooltip, Does.StartWith("The machine is keeping most of its cores parked while the rest are saturated, so work waits."));
        Assert.That(tooltip, Does.Contain("Usually heat: blocked vents, dusty fans, or a laptop on a soft surface."));
        Assert.That(tooltip, Does.Contain("Sometimes the power plan's minimum processor state."));
        Assert.That(tooltip, Does.Contain("16 of 20 parked; the rest 98% busy; clock 122% of base."));
    }

    [Test]
    public void TheTooltip_LeavesOutAClockItCouldNotRead()
    {
        Assert.That(CoreClampText.Tooltip(Clamp with { ClockPercent = null }), Does.EndWith("16 of 20 parked; the rest 98% busy."));
    }

    [Test]
    public void TheLog_RecordsTheFiguresWhenItStartsAndStops()
    {
        Assert.That(CoreClampText.Started(Clamp), Is.EqualTo("Cores held back: 16 of 20 parked, the rest 98% busy, clock 122% of base"));
        Assert.That(CoreClampText.Stopped(new CoreSample(20, 6, 40, 150)), Is.EqualTo("Cores no longer held back: 6 of 20 parked, the rest 40% busy, clock 150% of base"));
        Assert.That(CoreClampText.Stopped(null), Is.EqualTo("Cores no longer held back: the counters could not be read"));
    }
}
