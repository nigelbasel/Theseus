using Theseus.Core.Domain;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

[TestFixture]
public class StopRoutingTests
{
    [Test]
    public void For_LeavesAWorkingSessionToTheHook()
    {
        Assert.That(StopRouting.For(SessionLifecycle.Running, hookInstalled: true),
            Is.EqualTo(StopRoute.WaitForTurnEnd),
            "its turn ends soon, and a handoff written from a settled state beats one written mid-edit");
    }

    [Test]
    public void For_NudgesAnIdleSessionBecauseNothingElseWillStartATurn()
    {
        Assert.That(StopRouting.For(SessionLifecycle.Idle, hookInstalled: true), Is.EqualTo(StopRoute.Nudge));
    }

    [Test]
    public void For_NeverTypesIntoASessionWaitingOnItsUser()
    {
        Assert.That(StopRouting.For(SessionLifecycle.NeedsAttention, hookInstalled: true),
            Is.EqualTo(StopRoute.AnswerItFirst),
            "it is at a prompt, where a keystroke answers a question rather than writing a message");
    }

    [Test]
    public void For_FallsBackToTheClipboardWhenNoHookIsInstalled()
    {
        foreach (var lifecycle in new[]
                 {
                     SessionLifecycle.Running, SessionLifecycle.Idle, SessionLifecycle.NeedsAttention,
                 })
        {
            Assert.That(StopRouting.For(lifecycle, hookInstalled: false), Is.EqualTo(StopRoute.PasteYourself),
                "nothing would deliver the request, so asking would be a promise the board cannot keep");
        }
    }

    [Test]
    public void For_HasNothingToDoForASessionThatIsNotRunning()
    {
        foreach (var lifecycle in new[]
                 {
                     SessionLifecycle.NotStarted, SessionLifecycle.Ended, SessionLifecycle.Ghost,
                 })
        {
            Assert.That(StopRouting.For(lifecycle, hookInstalled: true), Is.EqualTo(StopRoute.None));
        }
    }

    [Test]
    public void For_TreatsASessionStillStartingAsOneThatWillTakeATurn()
    {
        Assert.That(StopRouting.For(SessionLifecycle.Launching, hookInstalled: true),
            Is.EqualTo(StopRoute.WaitForTurnEnd));
    }

    [Test]
    public void NudgeMarker_IsOneHarmlessWordAndNotTheRequest()
    {
        Assert.That(StopRouting.NudgeMarker, Does.Not.Contain(" "),
            "a keystroke landing somewhere unintended should leave one word, not a paragraph");
        Assert.That(StopRouting.NudgeMarker, Does.Contain("theseus"),
            "whoever finds it in a transcript or a shell should be able to tell where it came from");
    }

    [Test]
    public void For_CutsInWhenAWorkingSessionHasAlreadyBeenAskedOnce()
    {
        Assert.That(StopRouting.For(SessionLifecycle.Running, hookInstalled: true, alreadyAsked: true),
            Is.EqualTo(StopRoute.Interrupt),
            "asking twice is how somebody says they would rather have the session back than have it finish");
    }

    [Test]
    public void For_WaitsForTheTurnTheFirstTimeAWorkingSessionIsAsked()
    {
        Assert.That(StopRouting.For(SessionLifecycle.Running, hookInstalled: true, alreadyAsked: false),
            Is.EqualTo(StopRoute.WaitForTurnEnd));
    }

    [Test]
    public void For_StillOnlyNudgesAnIdleSessionAskedTwice()
    {
        Assert.That(StopRouting.For(SessionLifecycle.Idle, hookInstalled: true, alreadyAsked: true),
            Is.EqualTo(StopRoute.Nudge),
            "there is no turn to cut short; the keystroke that starts one is the whole job");
    }

    [Test]
    public void For_StillLeavesASessionWaitingOnItsUserAlone()
    {
        Assert.That(StopRouting.For(SessionLifecycle.NeedsAttention, hookInstalled: true, alreadyAsked: true),
            Is.EqualTo(StopRoute.AnswerItFirst));
    }

    [Test]
    public void For_NeedsAHookBeforeAskingTwiceMeansAnything()
    {
        Assert.That(StopRouting.For(SessionLifecycle.Running, hookInstalled: false, alreadyAsked: true),
            Is.EqualTo(StopRoute.PasteYourself));
    }
}
