using Theseus.Core.Breakers;
using Theseus.Core.Running;
using static Theseus.Tests.Running.RunRecords;

namespace Theseus.Tests.Breakers;

[TestFixture]
public class BreakerJudgeTests
{
    private static BreakerTrip? Judge(params RunRecord[] runs) => BreakerJudge.After(runs, BreakerSettings.Default, Noon);

    private static RunRecord At(int minute, RunOutcome outcome = RunOutcome.Completed, bool progress = true, string? error = null, bool handoff = false) =>
        Run(outcome: outcome, ended: Noon.AddMinutes(minute), progress: progress, error: error, handoff: handoff);

    [Test]
    public void ThreeCrashesInARow_TripTheCrashBreaker()
    {
        var trip = Judge(At(1, RunOutcome.Crashed), At(2, RunOutcome.FailedStart), At(3, RunOutcome.Crashed, error: "boom"));

        Assert.That(trip!.Kind, Is.EqualTo(BreakerKind.Crashes));
        Assert.That(trip.Detail, Does.Contain("3 crashes").And.Contain("boom"));
    }

    [Test]
    public void TwoCrashesAfterAGoodRun_DoNotTrip()
    {
        Assert.That(Judge(At(1, RunOutcome.Crashed), At(2), At(3, RunOutcome.Crashed), At(4, RunOutcome.Crashed)), Is.Null);
    }

    [Test]
    public void ThreeRunsWithoutProgress_TripTheNoProgressBreaker()
    {
        var trip = Judge(At(1, progress: false), At(2, progress: false), At(3, progress: false));

        Assert.That(trip!.Kind, Is.EqualTo(BreakerKind.NoProgress));
    }

    [Test]
    public void TheSameErrorThreeTimes_TripsTheSameErrorBreaker_BeforeNoProgress()
    {
        var trip = Judge(At(1, RunOutcome.Failed, false, "API Error: 500"), At(2, RunOutcome.Failed, false, "API Error: 500"),
            At(3, RunOutcome.Failed, false, "API Error: 500"));

        Assert.That(trip!.Kind, Is.EqualTo(BreakerKind.SameError));
        Assert.That(trip.Detail, Does.Contain("API Error: 500"));
    }

    [Test]
    public void DifferentErrors_DoNotTripTheSameErrorBreaker()
    {
        var trip = Judge(At(1, RunOutcome.Failed, error: "a"), At(2, RunOutcome.Failed, error: "b"), At(3, RunOutcome.Failed, error: "a"));

        Assert.That(trip, Is.Null);
    }

    [Test]
    public void ARunOverTime_TripsAtOnce()
    {
        Assert.That(Judge(At(1, RunOutcome.OverTime))!.Kind, Is.EqualTo(BreakerKind.OverTime));
    }

    [Test]
    public void AHandoffRun_IsNotCountedByTheBreakers()
    {
        var trip = Judge(At(1, progress: false), At(2, progress: false), At(3, progress: false, handoff: true));

        Assert.That(trip, Is.Null);
    }

    [Test]
    public void NoRuns_TripNothing()
    {
        Assert.That(Judge(), Is.Null);
    }
}
