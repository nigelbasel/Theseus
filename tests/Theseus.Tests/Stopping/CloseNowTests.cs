using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>
/// Close now: one more /exit attempt, only when pressed, only when the session is ready at that moment, and never
/// left for something else to retry later (close-step AC 9; verify part 1 findings F2 and F3).
/// </summary>
[TestFixture]
public class CloseNowTests
{
    private const string SessionId = "d616ea10-e23d-4b77-8157-c39be7095633";
    private static readonly DateTimeOffset Asked = new(2026, 11, 19, 13, 4, 0, TimeSpan.Zero);

    [Test]
    public void APressOnAStopThatTypedNothing_MakesTheAttempt()
    {
        var exits = Failed(ExitResult.FocusLost);
        var attempted = false;

        var decision = exits.TryCloseNow(Request(), () =>
        {
            attempted = true;

            return new CloseDecision(CloseVerdict.Close);
        });

        Assert.That(attempted, Is.True);
        Assert.That(decision?.Verdict, Is.EqualTo(CloseVerdict.Close));
    }

    [Test]
    public void APressWhenNotOffered_AttemptsNothing()
    {
        var attempted = false;

        var decision = Failed(ExitResult.Refused).TryCloseNow(Request(), () =>
        {
            attempted = true;

            return new CloseDecision(CloseVerdict.Close);
        });

        Assert.That(attempted, Is.False);
        Assert.That(decision, Is.Null);
    }

    [TestCase(CloseVerdict.Skip)]
    [TestCase(CloseVerdict.Wait)]
    [TestCase(CloseVerdict.Retire)]
    public void APressWhenTheSessionIsNotReady_LeavesNothingToRetryLater(CloseVerdict verdict)
    {
        var exits = Failed(ExitResult.FocusLost);

        exits.TryCloseNow(Request(), () => new CloseDecision(verdict));

        Assert.That(exits.AlreadyTyped(Request()), Is.True,
            "verify M19: otherwise the press's allowance would let the timer type /exit later, unasked");
        Assert.That(exits.MayCloseNow(Request()), Is.True, "and Close now can be pressed again");
    }

    [Test]
    public void NotReady_SaysSoAndThatItCanBePressedAgain()
    {
        Assert.That(CloseNowText.NotClosed("stop check", CloseVerdict.Skip),
            Is.EqualTo("stop check was not closed: it is not idle and quiet yet. Press Close now again once it is."));
        Assert.That(CloseNowText.NotClosed("stop check", CloseVerdict.Wait),
            Is.EqualTo("stop check was not closed: it is not idle and quiet yet. Press Close now again once it is."));
    }

    [Test]
    public void Retired_SaysTheStopWasAnEarlierRunsAndPromisesNothing()
    {
        Assert.That(CloseNowText.NotClosed("stop check", CloseVerdict.Retire),
            Is.EqualTo("stop check was not closed: its stop was handed to an earlier run of the session, so it has been retired."),
            "verify F3: 'can be pressed again' was false here");
    }

    private static ExitsTyped Failed(ExitResult result)
    {
        var exits = new ExitsTyped();
        exits.Attempted(Request(), new ExitAttempt(result, Asked.AddMinutes(2)));

        return exits;
    }

    private static PendingStopRequest Request() => new(SessionId, "stop-check", "stop check", "Please stop.",
        Asked, Asked.AddHours(4));
}
