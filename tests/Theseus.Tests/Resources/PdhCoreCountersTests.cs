using Theseus.Core.Resources;

namespace Theseus.Tests.Resources;

/// <summary>
/// Reading core parking from Windows' performance counters on the machine the tests run on (parked-cores spec AC 1).
/// The first read after opening has no rates yet; the second has.
/// </summary>
[TestFixture]
public class PdhCoreCountersTests
{
    [Test]
    public void TwoReads_GiveEveryLogicalProcessor_WithTheParkedOnesAmongThem()
    {
        using var counters = new PdhCoreCounters();

        var first = counters.Read();
        Thread.Sleep(1100);
        var second = counters.Read();

        if (second is null)
        {
            Assert.Ignore("The processor counters are not available on this machine.");
        }

        Assert.That(first, Is.Null, "the first read has no busy rates to give");
        Assert.That(second!.Total, Is.EqualTo(Environment.ProcessorCount));
        Assert.That(second.Parked, Is.InRange(0, second.Total));
        Assert.That(second.RunningBusyPercent, Is.InRange(0, 100));
    }
}
