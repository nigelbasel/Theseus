using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>
/// What Theseus remembers about the /exit it has typed. Typing it twice into the same stop would land in a
/// session that had moved on; never typing it for a later stop leaves that session waiting forever.
/// </summary>
[TestFixture]
public class ExitsTypedTests
{
    private const string SessionId = "d616ea10-e23d-4b77-8157-c39be7095633";
    private static readonly DateTimeOffset FirstStop = new(2026, 11, 18, 15, 19, 15, TimeSpan.Zero);

    [Test]
    public void AStopNotYetClosed_HasNoExitTyped()
    {
        Assert.That(new ExitsTyped().AlreadyTyped(Request(FirstStop)), Is.False);
    }

    [Test]
    public void AStopAlreadyClosed_IsNotTypedIntoAgain()
    {
        var exits = new ExitsTyped();
        var stop = Request(FirstStop);

        exits.Typed(stop);

        Assert.That(exits.AlreadyTyped(stop), Is.True);
    }

    [Test]
    public void NoStop_HasNoExitTyped()
    {
        Assert.That(new ExitsTyped().AlreadyTyped(null), Is.False);
    }

    private static PendingStopRequest Request(DateTimeOffset requested) =>
        new(SessionId, "stop-check-stays-idle", "Stop check: stays idle", "Write a handoff and stop.",
            requested, requested.AddHours(4));
}
