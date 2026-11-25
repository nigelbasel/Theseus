using Theseus.Core.Duties;

namespace Theseus.Tests.Duties;

/// <summary>Reading a cadence and an end from typed words.</summary>
[TestFixture]
public class DutyTextTests
{
    [TestCase("20m", 20)]
    [TestCase("20 min", 20)]
    [TestCase("90", 90)]
    [TestCase("2h", 120)]
    [TestCase("1.5 Hours", 90)]
    public void Interval_ReadsMinutesAndHours(string text, int minutes)
    {
        Assert.That(DutyText.Interval(text), Is.EqualTo(TimeSpan.FromMinutes(minutes)));
    }

    [Test]
    public void Interval_IsNull_ForWordsItCannotRead()
    {
        Assert.That(DutyText.Interval("soon"), Is.Null);
    }

    [Test]
    public void Times_ReadsAList_AndRefusesAnyItCannotRead()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DutyText.Times("07:00, 13:30"), Is.EqualTo(new[] { new TimeOnly(7, 0), new TimeOnly(13, 30) }));
            Assert.That(DutyText.Times("07:00, 7am"), Is.Null);
        });
    }

    [Test]
    public void Days_ReadsRangesListsAndEveryDay()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DutyText.Days("Mon-Fri"), Is.EqualTo(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }));
            Assert.That(DutyText.Days("Sat, sun"), Is.EqualTo(new[] { DayOfWeek.Saturday, DayOfWeek.Sunday }));
            Assert.That(DutyText.Days("Fri-Mon"), Is.EqualTo(new[] { DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday }));
            Assert.That(DutyText.Days("every day"), Is.Empty);
            Assert.That(DutyText.Days("someday"), Is.Null);
        });
    }

    [Test]
    public void End_ReadsNeverADateAndACriterion()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DutyText.End(null), Is.EqualTo(DutyEnd.Never));
            Assert.That(DutyText.End("2026-12-01"), Is.EqualTo(new DutyEnd(DutyEndKind.OnDate, new DateOnly(2026, 12, 1))));
            Assert.That(DutyText.End("criterion: it runs across a restart"), Is.EqualTo(new DutyEnd(DutyEndKind.OnCriterion, Criterion: "it runs across a restart")));
            Assert.That(DutyText.End("later"), Is.Null);
        });
    }
}
