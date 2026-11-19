using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>When the close step runs again on its own (close-step spec AC 1 and 2).</summary>
[TestFixture]
public class CloseRecheckTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 19, 13, 0, 0, TimeSpan.Zero);

    [Test]
    public void NothingWaiting_SchedulesNothing()
    {
        Assert.That(CloseRecheck.Next([]), Is.Null);
    }

    [Test]
    public void OneSessionWaiting_IsCheckedJustAfterItsQuietEnds()
    {
        Assert.That(CloseRecheck.Next([Now.AddSeconds(17)]), Is.EqualTo(Now.AddSeconds(17) + CloseRecheck.Margin));
    }

    [Test]
    public void SeveralWaiting_OnlyTheEarliestIsScheduled()
    {
        Assert.That(CloseRecheck.Next([Now.AddSeconds(15), Now.AddSeconds(4), Now.AddSeconds(9)]),
            Is.EqualTo(Now.AddSeconds(4) + CloseRecheck.Margin));
    }

    [Test]
    public void TheMargin_KeepsTheCloseWithinFiveSecondsOfQuiet()
    {
        Assert.That(CloseRecheck.Margin, Is.GreaterThan(TimeSpan.Zero).And.LessThanOrEqualTo(TimeSpan.FromSeconds(5)));
    }
}
