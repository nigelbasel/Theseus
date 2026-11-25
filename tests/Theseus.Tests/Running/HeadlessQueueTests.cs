using Theseus.Core.Running;

namespace Theseus.Tests.Running;

[TestFixture]
public class HeadlessQueueTests
{
    [Test]
    public void RunsUpToTheCap_StartAtOnce_AndTheRestQueueInOrder()
    {
        var queue = new HeadlessQueue(2);

        Assert.That(queue.Enter("a"), Is.Null);
        Assert.That(queue.Enter("b"), Is.Null);
        Assert.That(queue.Enter("c"), Is.EqualTo(1));
        Assert.That(queue.Enter("d"), Is.EqualTo(2));
        Assert.That((queue.Running, queue.Queued), Is.EqualTo((2, 2)));
    }

    [Test]
    public void WhenARunEnds_TheFirstQueuedTakesItsSlot()
    {
        var queue = new HeadlessQueue(1);
        queue.Enter("a");
        queue.Enter("b");
        queue.Enter("c");

        Assert.That(queue.TakeNext(), Is.Null);
        queue.Leave("a");
        Assert.That(queue.TakeNext(), Is.EqualTo("b"));
        Assert.That(queue.PositionOf("c"), Is.EqualTo(1));
    }

    [Test]
    public void ACardIsNeverQueuedTwice()
    {
        var queue = new HeadlessQueue(1);
        queue.Enter("a");
        queue.Enter("b");
        queue.Enter("b");

        Assert.That(queue.Queued, Is.EqualTo(1));
    }

    [Test]
    public void ANewcomer_DoesNotJumpTheQueue_WhenASlotHasJustFreed()
    {
        var queue = new HeadlessQueue(1);
        queue.Enter("a");
        queue.Enter("b");
        queue.Leave("a");

        Assert.That(queue.Enter("c"), Is.EqualTo(2));
        Assert.That(queue.TakeNext(), Is.EqualTo("b"));
    }
}
