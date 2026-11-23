using Theseus.Core.Links;

namespace Theseus.Tests.Links;

/// <summary>Which later reports the owner has opened, kept across restarts (Nigel, 2026-11-23: "Opening the report clears it").</summary>
[TestFixture]
public class ReportsOpenedTests
{
    private static readonly DateTimeOffset Noon = new(2026, 11, 23, 12, 0, 0, TimeSpan.Zero);
    private string _path = null!;

    [SetUp]
    public void SetUp() => _path = Path.Combine(Path.GetTempPath(), "theseus-opened-" + Guid.NewGuid().ToString("N") + ".json");

    [TearDown]
    public void TearDown() => File.Delete(_path);

    [Test]
    public void AnOpenedReport_StaysOpened_AcrossARestart_ButALaterOneDoesNot()
    {
        new ReportsOpened(_path).Open("lk-1", Noon);
        var again = new ReportsOpened(_path);

        Assert.Multiple(() =>
        {
            Assert.That(again.HasOpened("lk-1", Noon), Is.True);
            Assert.That(again.HasOpened("lk-1", Noon.AddMinutes(1)), Is.False, "a report filed after it shows again");
            Assert.That(again.HasOpened("lk-2", Noon), Is.False);
        });
    }
}
