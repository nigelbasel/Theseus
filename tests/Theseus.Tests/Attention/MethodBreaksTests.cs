using Theseus.Core.Attention;
using Theseus.Core.Persistence;

namespace Theseus.Tests.Attention;

/// <summary>7.3: the change of method is recorded once, dated, and attended time is labelled unmeasured until two clean weeks.</summary>
[TestFixture]
public class MethodBreaksTests
{
    private static readonly DateTimeOffset Break = new(2026, 11, 28, 8, 0, 0, TimeSpan.Zero);

    private string _path = null!;

    [SetUp]
    public void SetUp() => _path = Path.Combine(Path.GetTempPath(), $"theseus-methods-{Guid.NewGuid():N}.jsonl");

    [TearDown]
    public void TearDown() => File.Delete(_path);

    [Test]
    public void Ensure_RecordsTheBreakOnce_AtTheFirstStart()
    {
        var log = new JsonLinesFile<MethodBreak>(_path);

        MethodBreaks.Ensure(log, Break);
        var second = MethodBreaks.Ensure(log, Break.AddDays(1));

        Assert.That(second.Select(each => (each.Id, each.At)), Is.EqualTo(new[] { (MethodBreaks.AllTimeRecorded, Break) }));
        Assert.That(log.Read(), Has.Count.EqualTo(1));
    }

    [Test]
    public void AttendedIsUnmeasured_FromLateOctober_UntilTwoWeeksAfterTheBreak()
    {
        IReadOnlyList<MethodBreak> breaks = [new(MethodBreaks.AllTimeRecorded, Break, "x")];

        Assert.That(MethodBreaks.AttendedIsUnmeasured(new(2026, 10, 1, 0, 0, 0, TimeSpan.Zero), new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero), breaks),
            Is.False, "before late October it was measured");
        Assert.That(MethodBreaks.AttendedIsUnmeasured(Break.AddDays(3), Break.AddDays(4), breaks), Is.True, "not yet two clean weeks");
        Assert.That(MethodBreaks.AttendedIsUnmeasured(Break.AddDays(15), Break.AddDays(16), breaks), Is.False);
    }

    [Test]
    public void AttendedIsUnmeasured_WhileNoBreakIsRecorded()
    {
        Assert.That(MethodBreaks.AttendedIsUnmeasured(Break, Break.AddDays(60), []), Is.True);
    }

    [Test]
    public void Spanned_SaysTheMeasurementChangedOnTheDate_OnlyForARangeAcrossIt()
    {
        IReadOnlyList<MethodBreak> breaks = [new(MethodBreaks.AllTimeRecorded, Break, "machine time counts every session")];

        Assert.That(MethodBreaks.Spanned(Break.AddDays(-1), Break.AddDays(1), breaks).Single(),
            Is.EqualTo("Measurement changed on 2026-11-28: machine time counts every session."));
        Assert.That(MethodBreaks.Spanned(Break.AddDays(1), Break.AddDays(2), breaks), Is.Empty);
    }
}
