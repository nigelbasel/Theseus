using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

[TestFixture]
public class StopHookDecisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void Deliver_HandsOverTheRequestWaitingForThisSession()
    {
        Assert.That(StopHookDecision.Deliver(Input(), Waiting(), Now), Is.Not.Null);
    }

    [Test]
    public void Deliver_SaysNothingWhenNoRequestIsWaiting()
    {
        Assert.That(StopHookDecision.Deliver(Input(), null, Now), Is.Null,
            "the hook runs on every turn of every session on the machine, so silence is the default");
    }

    [Test]
    public void Deliver_SaysNothingInsideATurnAStopHookAlreadyExtended()
    {
        Assert.That(StopHookDecision.Deliver(Input(stopHookActive: true), Waiting(), Now), Is.Null,
            "delivering again inside the turn the hook itself caused would be a loop");
    }

    [Test]
    public void Deliver_SaysNothingToASubagent()
    {
        Assert.That(StopHookDecision.Deliver(Input(agentId: "a1b2c3"), Waiting(), Now), Is.Null,
            "a subagent is not the session the user asked to stop");
    }

    [Test]
    public void Deliver_DropsARequestThatHasBeenWaitingTooLong()
    {
        var stale = Waiting() with { ExpiresUtc = Now.AddMinutes(-1) };

        Assert.That(StopHookDecision.Deliver(Input(), stale, Now), Is.Null,
            "a request from last night must not interrupt a session resumed this morning");
    }

    [Test]
    public void Deliver_SaysNothingWhenTheInputCouldNotBeRead()
    {
        Assert.That(StopHookDecision.Deliver(null, Waiting(), Now), Is.Null);
    }

    private static StopHookInput Input(bool stopHookActive = false, string? agentId = null) =>
        new("11111111-2222-3333-4444-555555555555", "Stop", stopHookActive, agentId);

    private static PendingStopRequest Waiting() =>
        new("11111111-2222-3333-4444-555555555555", "contact-forms", "Contact forms",
            "Write a handoff and stop.", Now.AddMinutes(-5), Now.AddHours(2));
}
