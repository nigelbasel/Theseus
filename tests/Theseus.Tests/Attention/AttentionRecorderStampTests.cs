using Theseus.Core.Attention;

namespace Theseus.Tests.Attention;

/// <summary>7.3: every busy session counts, each line carries one stamp, gaps are kept, and what is open can be checkpointed.</summary>
[TestFixture]
public class AttentionRecorderStampTests
{
    private static readonly DateTimeOffset Zero = new(2026, 11, 27, 13, 0, 0, TimeSpan.Zero);
    private static readonly AttentionStamp Build = new("theseus", "piece-1", "Implement");
    private static readonly AttentionStamp Verify = new("theseus", "piece-1", "Verify");

    [Test]
    public void Observe_CountsTwoBusySessionsOfOneCardAsTwoMachineIntervals()
    {
        var recorder = new AttentionRecorder(TimeSpan.FromSeconds(20));

        recorder.Observe(Busy(0, ("alpha", "s1"), ("alpha", "s2")));
        recorder.Observe(Busy(5, ("alpha", "s1"), ("alpha", "s2")));
        var closed = recorder.Flush(At(10));

        Assert.That(closed.Select(interval => (interval.Workstream, interval.Session, interval.Length)),
            Is.EquivalentTo(new[] { ("alpha", (string?)"s1", TimeSpan.FromSeconds(10)), ("alpha", (string?)"s2", TimeSpan.FromSeconds(10)) }));
    }

    [Test]
    public void Observe_StampsEachLineWithItsPieceProjectStageSessionAndOffset()
    {
        var recorder = new AttentionRecorder(TimeSpan.FromSeconds(20));

        recorder.Observe(Busy(0, ("alpha", "s1")) with { BusySessions = [new BusySession("alpha", "s1", Build)], Offset = "+02:00" });
        var line = recorder.Flush(At(5)).Single();

        Assert.That(line, Is.EqualTo(new AttentionInterval(AttentionKind.Machine, "alpha", At(0), At(5))
        {
            Session = "s1",
            Project = "theseus",
            PieceId = "piece-1",
            Stage = "Implement",
            Offset = "+02:00",
        }));
    }

    [Test]
    public void Observe_ClosesAndReopensAnIntervalWhenItsStageChanges()
    {
        var recorder = new AttentionRecorder(TimeSpan.FromSeconds(20));

        recorder.Observe(Stamped(0, Build));
        recorder.Observe(Stamped(5, Build));
        var closed = recorder.Observe(Stamped(10, Verify)).ToList();
        closed.AddRange(recorder.Flush(At(15)));

        Assert.That(closed.Select(interval => (interval.Stage, interval.Start, interval.End)),
            Is.EqualTo(new[] { ((string?)"Implement", At(0), At(10)), ((string?)"Verify", At(10), At(15)) }),
            "every line carries one stamp that was true for all of it");
    }

    [Test]
    public void Observe_ClosesAndReopensWhenTheUtcOffsetChanges()
    {
        var recorder = new AttentionRecorder(TimeSpan.FromSeconds(20));

        recorder.Observe(new AttentionSample(At(0), "alpha", []) { Offset = "+02:00" });
        var closed = recorder.Observe(new AttentionSample(At(5), "alpha", []) { Offset = "+00:00" }).ToList();
        closed.AddRange(recorder.Flush(At(10)));

        Assert.That(closed.Select(interval => interval.Offset), Is.EqualTo(new[] { "+02:00", "+00:00" }));
    }

    [Test]
    public void Observe_KeepsAGapItClosesIntervalsOn()
    {
        var recorder = new AttentionRecorder(TimeSpan.FromSeconds(20));

        recorder.Observe(new AttentionSample(At(0), "alpha", []) { Offset = "+02:00" });
        recorder.Observe(new AttentionSample(At(300), "alpha", []) { Offset = "+02:00" });

        Assert.That(recorder.DrainGaps(), Is.EqualTo(new[] { new AttentionGap(At(0), At(300), AttentionGap.SamplerGap) { Offset = "+02:00" } }));
        Assert.That(recorder.DrainGaps(), Is.Empty, "each gap is given once");
    }

    [Test]
    public void Observe_KeepsNoGapWhileSamplesArriveOnTime()
    {
        var recorder = new AttentionRecorder(TimeSpan.FromSeconds(20));

        recorder.Observe(new AttentionSample(At(0), "alpha", []));
        recorder.Observe(new AttentionSample(At(15), "alpha", []));

        Assert.That(recorder.DrainGaps(), Is.Empty);
    }

    [Test]
    public void OpenIntervals_GivesWhatIsOpenToItsLastSighting_WithoutClosingIt()
    {
        var recorder = new AttentionRecorder(TimeSpan.FromSeconds(20));

        recorder.Observe(Busy(0, ("alpha", "s1")) with { Attending = "beta" });
        recorder.Observe(Busy(5, ("alpha", "s1")) with { Attending = "beta" });

        Assert.That(recorder.OpenIntervals().Select(interval => (interval.Kind, interval.Workstream, interval.End)),
            Is.EquivalentTo(new[] { (AttentionKind.Attended, "beta", At(5)), (AttentionKind.Machine, "alpha", At(5)) }));
        Assert.That(recorder.Flush(At(10)).Select(interval => interval.End), Is.All.EqualTo(At(10)), "asking closed nothing");
    }

    private static AttentionSample Busy(int seconds, params (string Card, string Session)[] sessions) =>
        new(At(seconds), null, [.. sessions.Select(session => session.Card).Distinct()])
        {
            BusySessions = [.. sessions.Select(session => new BusySession(session.Card, session.Session))],
        };

    private static AttentionSample Stamped(int seconds, AttentionStamp stamp) =>
        new(At(seconds), null, ["alpha"]) { BusySessions = [new BusySession("alpha", "s1", stamp)] };

    private static DateTimeOffset At(int seconds) => Zero.AddSeconds(seconds);
}
