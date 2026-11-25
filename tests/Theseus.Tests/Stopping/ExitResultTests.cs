using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>
/// What an attempt to type /exit did, and when Close now may try again (close-step spec AC 9 and 11). Every attempt
/// raises the terminal, so nothing retries on its own; and an attempt that may have typed part of /exit is never
/// repeated.
/// </summary>
[TestFixture]
public class ExitResultTests
{
    private const string SessionId = "d616ea10-e23d-4b77-8157-c39be7095633";
    private static readonly DateTimeOffset Asked = new(2026, 11, 19, 13, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Tried = Asked.AddMinutes(1);

    [TestCase(ExitResult.TabNotConfirmed)]
    [TestCase(ExitResult.FocusLost)]
    public void NothingTyped_CloseNowMayTryAgain(ExitResult result)
    {
        var exits = Attempted(result);

        Assert.That(exits.MayCloseNow(Request()), Is.True);
    }

    [TestCase(ExitResult.Sent)]
    [TestCase(ExitResult.Refused)]
    [TestCase(ExitResult.Elevated)]
    public void SentOrPossiblyPartlyTyped_CloseNowIsNeverOffered(ExitResult result)
    {
        Assert.That(Attempted(result).MayCloseNow(Request()), Is.False,
            "a second /exit after a sent or partial one would be typed into the prompt");
    }

    [Test]
    public void NoAttemptYet_HasNothingForCloseNow()
    {
        Assert.That(new ExitsTyped().MayCloseNow(Request()), Is.False);
    }

    [Test]
    public void AnAttempt_IsRememberedWithItsResultAndTime()
    {
        var attempt = Attempted(ExitResult.FocusLost).LastAttempt(Request());

        Assert.That(attempt, Is.EqualTo(new ExitAttempt(ExitResult.FocusLost, Tried)));
    }

    [Test]
    public void AFailedAttempt_StillCountsAsTyped_SoNothingRetriesItOnItsOwn()
    {
        Assert.That(Attempted(ExitResult.FocusLost).AlreadyTyped(Request()), Is.True);
    }

    [Test]
    public void CloseNow_AllowsExactlyOneMoreAttempt()
    {
        var exits = Attempted(ExitResult.FocusLost);

        Assert.That(exits.AllowCloseNow(Request()), Is.True);
        Assert.That(exits.AlreadyTyped(Request()), Is.False, "the press lets the close step try once more");

        exits.Attempted(Request(), new ExitAttempt(ExitResult.FocusLost, Tried.AddMinutes(1)));

        Assert.That(exits.AlreadyTyped(Request()), Is.True, "and only once per press");
    }

    [Test]
    public void CloseNow_IsRefusedWhenNotOffered()
    {
        var exits = Attempted(ExitResult.Refused);

        Assert.That(exits.AllowCloseNow(Request()), Is.False);
        Assert.That(exits.AlreadyTyped(Request()), Is.True);
    }

    [Test]
    public void ASentAttempt_FinishesTheStop()
    {
        Assert.That(Attempted(ExitResult.Sent).IsFinished(Request()), Is.True);
        Assert.That(Attempted(ExitResult.FocusLost).IsFinished(Request()), Is.False);
    }

    private static ExitsTyped Attempted(ExitResult result)
    {
        var exits = new ExitsTyped();
        exits.Attempted(Request(), new ExitAttempt(result, Tried));

        return exits;
    }

    private static PendingStopRequest Request() => new(SessionId, "stop-check", "stop check", "Please stop.",
        Asked, Asked.AddHours(4));
}
