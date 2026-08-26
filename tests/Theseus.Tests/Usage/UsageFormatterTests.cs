using Theseus.Core.Usage;

namespace Theseus.Tests.Usage;

[TestFixture]
public class UsageFormatterTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 26, 9, 34, 0, TimeSpan.Zero);

    [Test]
    public void Label_NamesTheFiveHourWindowShortEnoughForAStatusBar()
    {
        Assert.That(UsageFormatter.Label(Limit("session")), Is.EqualTo("5h"));
    }

    [Test]
    public void Label_NamesTheSevenDayWindow()
    {
        Assert.That(UsageFormatter.Label(Limit("weekly_all")), Is.EqualTo("7d"));
    }

    [Test]
    public void Label_PrefersTheModelNameForAScopedWindowBecauseThatIsWhatIsBinding()
    {
        Assert.That(UsageFormatter.Label(Limit("weekly_scoped", scope: "Fable")), Is.EqualTo("Fable"));
    }

    [Test]
    public void Label_FallsBackToAReadableFormOfAnUnknownWindow()
    {
        Assert.That(UsageFormatter.Label(Limit("seven_day_cowork")), Is.EqualTo("seven day cowork"));
    }

    [Test]
    public void Percent_RoundsToAWholeNumber()
    {
        Assert.That(UsageFormatter.Percent(Limit("session", percent: 66.4)), Is.EqualTo("66%"));
        Assert.That(UsageFormatter.Percent(Limit("session", percent: 66.5)), Is.EqualTo("67%"));
    }

    [Test]
    public void Reset_CountsDownInMinutesWithinTheHour()
    {
        var limit = Limit("session", resets: Now.AddMinutes(56));

        Assert.That(UsageFormatter.Reset(limit, Now), Is.EqualTo("resets in 56m"));
    }

    [Test]
    public void Reset_CountsDownInHoursAndMinutesWithinTheDay()
    {
        var limit = Limit("session", resets: Now.AddHours(3).AddMinutes(12));

        Assert.That(UsageFormatter.Reset(limit, Now), Is.EqualTo("resets in 3h 12m"));
    }

    [Test]
    public void Reset_OmitsTheMinutesOnAWholeNumberOfHours()
    {
        Assert.That(UsageFormatter.Reset(Limit("session", resets: Now.AddHours(4)), Now),
            Is.EqualTo("resets in 4h"));
    }

    [Test]
    public void Reset_NamesTheDayOnceItIsMoreThanADayAwayBecauseACountdownStopsMeaningAnything()
    {
        var text = UsageFormatter.Reset(Limit("weekly_all", resets: Now.AddDays(4)), Now);

        Assert.That(text, Does.StartWith("resets "));
        Assert.That(text, Does.Not.Contain("in "));
    }

    [Test]
    public void Reset_SaysResettingRatherThanShowingNegativeTime()
    {
        Assert.That(UsageFormatter.Reset(Limit("session", resets: Now.AddMinutes(-1)), Now),
            Is.EqualTo("resetting"));
    }

    [Test]
    public void Reset_IsEmptyWhenTheWindowHasNoKnownReset()
    {
        Assert.That(UsageFormatter.Reset(Limit("session"), Now), Is.Empty);
    }

    [Test]
    public void Describe_ReadsAsOneLine()
    {
        var limit = Limit("session", percent: 66, resets: Now.AddMinutes(56));

        Assert.That(UsageFormatter.Describe(limit, Now), Is.EqualTo("5h 66% · resets in 56m"));
    }

    [Test]
    public void Describe_OmitsTheResetWhenThereIsNoneRatherThanTrailingASeparator()
    {
        Assert.That(UsageFormatter.Describe(Limit("session", percent: 66), Now), Is.EqualTo("5h 66%"));
    }

    private static UsageLimit Limit(
        string kind,
        double percent = 50,
        DateTimeOffset? resets = null,
        string? scope = null) =>
        new(kind, "group", percent, "normal", resets, scope, IsActive: false);
}
