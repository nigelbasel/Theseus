using Theseus.Core.Domain;
using Theseus.Core.Mcp;

namespace Theseus.Tests.Mcp;

[TestFixture]
public class LiveStartTests
{
    [Test]
    public void AnIdleOpenSessionGivenAnInstruction_TakesItAsAHandIn()
    {
        Assert.Multiple(() =>
        {
            Assert.That(LiveStart.IsHandIn(true, SessionLifecycle.Idle, "more work"), Is.True);
            Assert.That(LiveStart.Refusal("Card", true, SessionLifecycle.Idle, "more work"), Is.Null);
        });
    }

    [Test]
    public void ACardWithNoSession_IsNotRefusedHere_ItIsStarted()
    {
        Assert.That((LiveStart.IsHandIn(false, SessionLifecycle.Idle, "more work"), LiveStart.Refusal("Card", false, SessionLifecycle.Idle, null)),
            Is.EqualTo((false, (string?)null)));
    }

    [TestCase(SessionLifecycle.Running, "more work", "Card is running.")]
    [TestCase(SessionLifecycle.Launching, "more work", "Card is starting.")]
    [TestCase(SessionLifecycle.NeedsAttention, "more work", "Card is waiting for the user to answer it.")]
    [TestCase(SessionLifecycle.Idle, " ", "Card is open and idle: give it an instruction, and it is handed in as its next turn.")]
    public void OnlyASessionInTheMiddleOfSomething_IsRunning(SessionLifecycle lifecycle, string instruction, string said)
    {
        Assert.That(LiveStart.Refusal("Card", true, lifecycle, instruction), Is.EqualTo(said));
    }
}
