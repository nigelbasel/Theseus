using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>
/// What the banner says when Stop is pressed on sessions already stopping, some of whose /exit did not land
/// (close-step AC 10; verify part 1 findings F4 and M22).
/// </summary>
[TestFixture]
public class SecondPressNoticeTests
{
    private static readonly DateTimeOffset Tried = new(2026, 11, 19, 13, 6, 0, TimeSpan.Zero);
    private const string Described = "One session will be asked to write its handoff and exit when its turn ends.";

    [Test]
    public void NoFailedExit_IsTheUsualDescription()
    {
        Assert.That(SecondPressNotice.For([], Described, othersDescribed: true, TimeZoneInfo.Utc), Is.EqualTo(Described));
    }

    [Test]
    public void AFailedExit_NamesTheSessionAndSaysWhatItsCardSays()
    {
        var notice = SecondPressNotice.For([("stop check", Failed(ExitResult.FocusLost))], Described,
            othersDescribed: false, TimeZoneInfo.Utc);

        Assert.That(notice, Is.EqualTo("stop check: Could not type /exit: the terminal was not in front. Close now tries again."));
    }

    [Test]
    public void AFailedExitAmongOthers_IsFollowedByTheUsualDescription()
    {
        var notice = SecondPressNotice.For([("stop check", Failed(ExitResult.Elevated))], Described,
            othersDescribed: true, TimeZoneInfo.Utc);

        Assert.That(notice, Is.EqualTo(
            "stop check: Could not type /exit: the terminal runs as administrator and Theseus does not. Close it yourself. " + Described));
    }

    [Test]
    public void OnlyAFailedExit_CountsAsStuck()
    {
        Assert.That(SecondPressNotice.IsStuck(Failed(ExitResult.Refused)), Is.True);
        Assert.That(SecondPressNotice.IsStuck(new StopState(StopStage.ExitTyped, Tried)), Is.False);
        Assert.That(SecondPressNotice.IsStuck(new StopState(StopStage.HandedOver, Tried)), Is.False);
        Assert.That(SecondPressNotice.IsStuck(null), Is.False);
    }

    private static StopState Failed(ExitResult result) => new(StopStage.ExitFailed, Tried, result);
}
