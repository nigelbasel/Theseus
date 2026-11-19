using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>
/// What a card shows for its session's stop: whether one is under way, how far it has got, and whether Close now is
/// offered (close-step AC 5, 6 and 9; verify part 1 findings F1, F2 and F7).
/// </summary>
[TestFixture]
public class StopViewTests
{
    private const string SessionId = "d616ea10-e23d-4b77-8157-c39be7095633";
    private static readonly DateTimeOffset Asked = new(2026, 11, 19, 13, 4, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Started = Asked.AddHours(-1);

    [Test]
    public void NoStop_ShowsNothing()
    {
        var view = StopView.Of(null, null, null, new ExitsTyped(), SessionId, Started, heldBack: false);

        Assert.That(view.State, Is.Null);
        Assert.That(view.CanCloseNow, Is.False);
    }

    [Test]
    public void AfterExitIsSent_TheCardStaysClosing_ThoughTheRequestIsGone()
    {
        var exits = new ExitsTyped();
        exits.Attempted(Request(), new ExitAttempt(ExitResult.Sent, Asked.AddMinutes(2)));

        var view = StopView.Of(null, null, null, exits, SessionId, Started, heldBack: false);

        Assert.That(view.State, Is.EqualTo(new StopState(StopStage.ExitTyped, Asked.AddMinutes(2))),
            "verify F1: the request is retired the moment /exit is sent, and the Stop icon came back while it closed");
    }

    [Test]
    public void ASessionResumedAfterItsExit_IsNoLongerClosing()
    {
        var exits = new ExitsTyped();
        exits.Attempted(Request(), new ExitAttempt(ExitResult.Sent, Asked.AddMinutes(2)));

        var view = StopView.Of(null, null, null, exits, SessionId, Asked.AddMinutes(30), heldBack: false);

        Assert.That(view.State, Is.Null, "the run that was closed has gone; this one was never asked to stop");
    }

    [Test]
    public void AFailedExit_ThatTypedNothing_OffersCloseNow()
    {
        var view = StopView.Of(null, Request(), Asked.AddMinutes(1), Failed(ExitResult.FocusLost), SessionId, Started,
            heldBack: false);

        Assert.That(view.CanCloseNow, Is.True);
    }

    [TestCase(ExitResult.Refused)]
    [TestCase(ExitResult.Elevated)]
    public void AFailedExit_ThatMayHaveTypedSomething_NeverOffersCloseNow(ExitResult result)
    {
        var view = StopView.Of(null, Request(), Asked.AddMinutes(1), Failed(result), SessionId, Started, heldBack: false);

        Assert.That(view.CanCloseNow, Is.False);
    }

    [Test]
    public void AStopWithNoFailedExit_NeverOffersCloseNow()
    {
        var view = StopView.Of(null, Request(), Asked.AddMinutes(1), new ExitsTyped(), SessionId, Started, heldBack: false);

        Assert.That(view.CanCloseNow, Is.False, "verify M21: Close now was offered for any stop");
    }

    [Test]
    public void AHeldBackStop_NeverOffersCloseNow_AndSaysItWaitsForCloseThem()
    {
        var view = StopView.Of(null, Request(), Asked.AddMinutes(1), Failed(ExitResult.FocusLost), SessionId, Started,
            heldBack: true);

        Assert.That(view.CanCloseNow, Is.False);
        Assert.That(StopStageText.For(view, TimeZoneInfo.Utc),
            Is.EqualTo("Held back when the board opened. Close them, in the banner, finishes this stop."),
            "verify F7: it does not close after 20 s of quiet until the owner agrees");
    }

    [Test]
    public void AStopThatIsNotHeldBack_IsWordedByItsStage()
    {
        var view = StopView.Of(Request(), null, null, new ExitsTyped(), SessionId, Started, heldBack: false);

        Assert.That(StopStageText.For(view, TimeZoneInfo.Utc), Is.EqualTo("Asked at 13:04. Waiting for its turn to end."));
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
