using Theseus.Core.Domain;
using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class SessionActivityTests
{
    [Test]
    public void For_ReadsASessionThatIsWorking()
    {
        Assert.That(SessionActivity.For("busy"), Is.EqualTo(SessionLifecycle.Running));
    }

    [Test]
    public void For_ReadsASessionThatIsWaitingForAPerson()
    {
        Assert.That(SessionActivity.For("waiting"), Is.EqualTo(SessionLifecycle.NeedsAttention),
            "a session waiting for a reply is the one thing on the board asking for someone");
    }

    [Test]
    public void For_ReadsASessionThatIsSittingIdle()
    {
        Assert.That(SessionActivity.For("idle"), Is.EqualTo(SessionLifecycle.Idle));
    }

    [Test]
    public void For_IgnoresCasingAndSurroundingSpace()
    {
        Assert.That(SessionActivity.For(" Waiting "), Is.EqualTo(SessionLifecycle.NeedsAttention));
    }

    [Test]
    public void For_AssumesAStatusItDoesNotRecogniseIsWork()
    {
        Assert.That(SessionActivity.For("compacting"), Is.EqualTo(SessionLifecycle.Running),
            "a new status should read as running rather than as needing attention it may not need");
        Assert.That(SessionActivity.For(null), Is.EqualTo(SessionLifecycle.Running));
    }
}
