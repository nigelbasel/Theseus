using Theseus.Core.Duties;

namespace Theseus.Tests.Duties;

/// <summary>Due times are a grid from the anchor or the set times, so a late or missed run never shifts the ones after it.</summary>
[TestFixture]
public class DutyCadenceTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;
    private static readonly DateTimeOffset SetAt = new(2026, 11, 24, 21, 30, 0, TimeSpan.Zero);

    [Test]
    public void Every_CountsFromWhenItWasSet()
    {
        var cadence = DutyCadence.Every(TimeSpan.FromHours(2));

        Assert.Multiple(() =>
        {
            Assert.That(cadence.NextAfter(SetAt, SetAt, Utc), Is.EqualTo(SetAt.AddHours(2)));
            Assert.That(cadence.NextAfter(SetAt, SetAt.AddHours(2), Utc), Is.EqualTo(SetAt.AddHours(4)), "strictly after");
            Assert.That(cadence.NextAfter(SetAt, SetAt.AddHours(5).AddMinutes(1), Utc), Is.EqualTo(SetAt.AddHours(6)));
        });
    }

    [Test]
    public void At_GivesTheNextSetTime_OnAChosenDay()
    {
        var cadence = DutyCadence.At([new TimeOnly(7, 0), new TimeOnly(13, 0)], [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]);
        var tuesdayEvening = new DateTimeOffset(2026, 11, 24, 21, 30, 0, TimeSpan.Zero);
        var fridayAfternoon = new DateTimeOffset(2026, 11, 27, 14, 0, 0, TimeSpan.Zero);

        Assert.Multiple(() =>
        {
            Assert.That(cadence.NextAfter(SetAt, tuesdayEvening, Utc), Is.EqualTo(new DateTimeOffset(2026, 11, 25, 7, 0, 0, TimeSpan.Zero)));
            Assert.That(cadence.NextAfter(SetAt, fridayAfternoon, Utc), Is.EqualTo(new DateTimeOffset(2026, 11, 30, 7, 0, 0, TimeSpan.Zero)), "skips the weekend");
            Assert.That(cadence.Describe(), Is.EqualTo("at 07:00, 13:00 on Mon–Fri"));
        });
    }

    [Test]
    public void CountBetween_CountsTheDueTimesMissed()
    {
        var cadence = DutyCadence.Every(TimeSpan.FromMinutes(20));

        Assert.That(cadence.CountBetween(SetAt, SetAt.AddMinutes(20), SetAt.AddMinutes(100), Utc), Is.EqualTo(4));
    }

    [Test]
    public void Refusal_TurnsDownAnIntervalUnderFiveMinutes_AndAtWithNoTimes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DutyCadence.Every(TimeSpan.FromMinutes(4)).Refusal(), Is.Not.Null);
            Assert.That(DutyCadence.Every(TimeSpan.FromMinutes(5)).Refusal(), Is.Null);
            Assert.That(DutyCadence.At([]).Refusal(), Is.Not.Null);
        });
    }

    [Test]
    public void Equality_IsByValue_ListsIncluded()
    {
        var first = DutyCadence.At([new TimeOnly(7, 0)], [DayOfWeek.Monday]);
        var second = DutyCadence.At([new TimeOnly(7, 0)], [DayOfWeek.Monday]);

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void Describe_SaysMinutesOrHours()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DutyCadence.Every(TimeSpan.FromMinutes(20)).Describe(), Is.EqualTo("every 20 min"));
            Assert.That(DutyCadence.Every(TimeSpan.FromHours(2)).Describe(), Is.EqualTo("every 2 h"));
        });
    }
}
