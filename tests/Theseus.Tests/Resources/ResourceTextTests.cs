using Theseus.Core.Resources;

namespace Theseus.Tests.Resources;

[TestFixture]
public class ResourceTextTests
{
    private static readonly SessionResources Busy = new(9, 12.4, (long)(1.4 * (1L << 30)), 230, [
        new ProcessShare("dotnet ×2", 8, 900L << 20),
        new ProcessShare("claude", 4, 500L << 20),
    ]);

    [Test]
    public void Card_SaysProcessorMemoryIoAndAgents()
    {
        Assert.That(ResourceText.Card(Busy, hasRates: true, agents: 2), Is.EqualTo("CPU 12% · 1.4 GB · 230 I/O/s · 2 agents"));
    }

    [Test]
    public void Card_BeforeAnyRateIsKnown_SaysOnlyMemory()
    {
        Assert.That(ResourceText.Card(Busy, hasRates: false, agents: 0), Is.EqualTo("1.4 GB"));
    }

    [Test]
    public void Tooltip_ListsTheBusiestProcesses()
    {
        var tooltip = ResourceText.Tooltip(Busy, hasRates: true);

        Assert.That(tooltip, Does.StartWith("9 processes in this session's tree:"));
        Assert.That(tooltip, Does.Contain("dotnet ×2  CPU 8%  900 MB"));
    }

    [Test]
    public void Machine_NamesDefenderAndDockerBesideTheTotal()
    {
        Assert.That(ResourceText.Machine(new MachineResources(48.2, 6.5, 12, 30), hasRates: true),
            Is.EqualTo("CPU 48% · sessions 30% · Defender 7% · Docker 12%"));
        Assert.That(ResourceText.Machine(new MachineResources(0, 0, 0, 0), hasRates: false), Is.Empty);
    }

    [TestCase(0, "0%")]
    [TestCase(0.3, "<1%")]
    [TestCase(57.6, "58%")]
    public void Percent_KeepsASmallShareVisible(double percent, string expected) =>
        Assert.That(ResourceText.Percent(percent), Is.EqualTo(expected));

    [TestCase(350L << 20, "350 MB")]
    [TestCase(3L << 30, "3.0 GB")]
    public void Bytes_UsesTheShortUnit(long bytes, string expected) =>
        Assert.That(ResourceText.Bytes(bytes), Is.EqualTo(expected));
}
