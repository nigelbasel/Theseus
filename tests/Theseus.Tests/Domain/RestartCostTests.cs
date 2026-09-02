using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class RestartCostTests
{
    private const long Megabyte = 1024 * 1024;

    [Test]
    public void Describe_SaysHowMuchOfTheTranscriptAResumeWorksThrough()
    {
        // The largest transcript on this machine: 30 MB, compacted once near its start.
        Assert.That(RestartCost.Describe(23 * Megabyte, 30 * Megabyte),
            Is.EqualTo("most of a 30 MB transcript"));
    }

    [Test]
    public void Describe_SaysSoWhenCompactionHasTakenNearlyAllOfItAway()
    {
        Assert.That(RestartCost.Describe(3 * Megabyte, 23 * Megabyte),
            Does.StartWith("some"), "a session mostly compacted away is cheap to resume");
    }

    [Test]
    public void Describe_SaysNearlyAllForATranscriptNeverCompacted()
    {
        Assert.That(RestartCost.Describe(20 * Megabyte, 20 * Megabyte),
            Does.StartWith("nearly all"));
    }

    [Test]
    public void Describe_SaysNothingWhenThereIsNothingToMeasure()
    {
        Assert.Multiple(() =>
        {
            Assert.That(RestartCost.Describe(0, 0), Is.Empty,
                "silence is better than telling somebody a resume is free");
            Assert.That(RestartCost.Describe(100, 0), Is.Empty);
            Assert.That(RestartCost.Describe(0, 100), Is.Empty);
        });
    }

    [Test]
    public void Tooltip_GivesBothTheProportionAndTheSize()
    {
        var said = RestartCost.Tooltip(23 * Megabyte, 30 * Megabyte);

        Assert.Multiple(() =>
        {
            Assert.That(said, Does.Contain("most of a 30 MB transcript"));
            Assert.That(said, Does.Contain("23 MB since its last compaction"));
            Assert.That(said, Does.Contain("context"), "the cost is where it starts, not only what it reads");
        });
    }

    [Test]
    public void Tooltip_StillExplainsTheButtonWhenThereIsNothingToMeasure()
    {
        Assert.That(RestartCost.Tooltip(0, 0), Is.Not.Empty,
            "a button with no tooltip is worse than one with a plain description");
        Assert.That(RestartCost.Tooltip(0, 0), Does.Not.Contain("transcript"));
    }

    [TestCase(0L, "0 MB")]
    [TestCase(2048L, "2 KB")]
    [TestCase(3L * 1024 * 1024, "3 MB")]
    [TestCase(30L * 1024 * 1024, "30 MB")]
    public void Size_ReadsTheWaySomebodyWouldSayIt(long bytes, string expected)
    {
        Assert.That(RestartCost.Size(bytes), Is.EqualTo(expected));
    }

    [Test]
    public void Size_KeepsOneDecimalOnlyWhereItMeansSomething()
    {
        Assert.That(RestartCost.Size((long)(3.8 * Megabyte)), Is.EqualTo("3.8 MB"));
        Assert.That(RestartCost.Size(23 * Megabyte), Is.EqualTo("23 MB"),
            "a tenth of a megabyte at this size is noise");
    }
}
