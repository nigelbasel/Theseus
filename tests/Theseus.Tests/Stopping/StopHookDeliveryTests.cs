using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

[TestFixture]
public class StopHookDeliveryTests
{
    [Test]
    public void For_RefusesTheEndingOfATurnRatherThanTalkingOverIt()
    {
        var delivery = StopHookDelivery.For("Stop", "write a handoff");

        Assert.Multiple(() =>
        {
            Assert.That(delivery.ExitCode, Is.EqualTo(2),
                "only a refusal reaches the session; anything else is read and discarded");
            Assert.That(delivery.StandardError, Is.EqualTo("write a handoff"));
            Assert.That(delivery.StandardOutput, Is.Empty);
        });
    }

    [Test]
    public void For_RefusesASubagentsEndingTheSameWay()
    {
        Assert.That(StopHookDelivery.For("SubagentStop", "stop").ExitCode, Is.EqualTo(2));
    }

    [Test]
    public void For_HandsAPromptItsContextWithoutBlockingAnything()
    {
        var delivery = StopHookDelivery.For("UserPromptSubmit", "write a handoff");

        Assert.Multiple(() =>
        {
            Assert.That(delivery.ExitCode, Is.Zero,
                "blocking here would erase what the user just typed");
            Assert.That(delivery.StandardOutput, Does.Contain("additionalContext"));
            Assert.That(delivery.StandardOutput, Does.Contain("UserPromptSubmit"));
            Assert.That(delivery.StandardError, Is.Empty);
        });
    }

    [Test]
    public void For_FallsBackToContextForAnEventItWasNotToldTheNameOf()
    {
        var delivery = StopHookDelivery.For(null, "write a handoff");

        Assert.That(delivery.ExitCode, Is.Zero, "never block a turn on a guess");
        Assert.That(delivery.StandardOutput, Does.Contain("additionalContext"));
    }

    [Test]
    public void Silent_SaysNothingAndLetsTheTurnEnd()
    {
        Assert.Multiple(() =>
        {
            Assert.That(StopHookDelivery.Silent.ExitCode, Is.Zero);
            Assert.That(StopHookDelivery.Silent.StandardOutput, Is.Empty);
            Assert.That(StopHookDelivery.Silent.StandardError, Is.Empty);
        });
    }
}
