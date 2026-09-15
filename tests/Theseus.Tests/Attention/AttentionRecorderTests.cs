using Theseus.Core.Attention;

namespace Theseus.Tests.Attention;

[TestFixture]
public class AttentionRecorderTests
{
    private static readonly DateTimeOffset Zero = new(2026, 9, 15, 13, 0, 0, TimeSpan.Zero);

    [Test]
    public void Observe_KeepsOneIntervalOpenWhileTheSameTabStaysInFront()
    {
        var recorder = new AttentionRecorder(TimeSpan.FromSeconds(20));

        var closed = new List<AttentionInterval>();
        closed.AddRange(recorder.Observe(Sample(0, "alpha")));
        closed.AddRange(recorder.Observe(Sample(5, "alpha")));
        closed.AddRange(recorder.Observe(Sample(10, "alpha")));
        closed.AddRange(recorder.Observe(Sample(15, "beta")));

        Assert.That(closed, Is.EqualTo(new[] { Attended("alpha", 0, 15) }));
    }

    [Test]
    public void Observe_NeverLetsTwoAttendedIntervalsOverlap()
    {
        var recorder = new AttentionRecorder(TimeSpan.FromSeconds(20));
        var closed = new List<AttentionInterval>();
        string?[] focus = ["alpha", "beta", "beta", null, "alpha", "gamma", "alpha"];

        for (var index = 0; index < focus.Length; index++)
        {
            closed.AddRange(recorder.Observe(Sample(index * 5, focus[index])));
        }

        closed.AddRange(recorder.Flush(At(focus.Length * 5)));

        var attended = closed.Where(interval => interval.Kind == AttentionKind.Attended).OrderBy(interval => interval.Start).ToList();

        Assert.That(attended.Zip(attended.Skip(1)).All(pair => pair.First.End <= pair.Second.Start), Is.True);
        Assert.That(attended.Sum(interval => interval.Length.TotalSeconds), Is.LessThanOrEqualTo(focus.Length * 5),
            "attended time can never exceed the wall clock");
    }

    [Test]
    public void Observe_DoesNotCountAGapWhenSamplesStop()
    {
        var recorder = new AttentionRecorder(TimeSpan.FromSeconds(20));

        recorder.Observe(Sample(0, "alpha"));
        recorder.Observe(Sample(5, "alpha"));
        var closed = recorder.Observe(Sample(300, "alpha"));

        Assert.That(closed, Is.EqualTo(new[] { Attended("alpha", 0, 5) }),
            "an asleep machine is not five minutes of attention");
        Assert.That(recorder.Flush(At(310)), Is.EqualTo(new[] { Attended("alpha", 300, 310) }));
    }

    [Test]
    public void Observe_CountsEveryBusyWorkstreamAsMachineTimeAtOnce()
    {
        var recorder = new AttentionRecorder(TimeSpan.FromSeconds(20));

        recorder.Observe(Sample(0, null, "alpha", "beta"));
        recorder.Observe(Sample(5, null, "alpha", "beta"));
        var closed = recorder.Observe(Sample(10, null, "alpha"));

        Assert.That(closed, Is.EqualTo(new[] { Machine("beta", 0, 10) }));
        Assert.That(recorder.Flush(At(20)), Is.EqualTo(new[] { Machine("alpha", 0, 20) }));
    }

    [Test]
    public void Observe_RecordsNoAttentionWhileNothingIsAttended()
    {
        var recorder = new AttentionRecorder(TimeSpan.FromSeconds(20));

        recorder.Observe(Sample(0, null));
        recorder.Observe(Sample(5, null));

        Assert.That(recorder.Flush(At(10)), Is.Empty);
    }

    [Test]
    public void Flush_ClosesNothingTwice()
    {
        var recorder = new AttentionRecorder(TimeSpan.FromSeconds(20));

        recorder.Observe(Sample(0, "alpha", "alpha"));

        Assert.That(recorder.Flush(At(5)), Has.Count.EqualTo(2));
        Assert.That(recorder.Flush(At(10)), Is.Empty);
    }

    private static AttentionSample Sample(int seconds, string? attending, params string[] busy) =>
        new(At(seconds), attending, busy);

    private static AttentionInterval Attended(string workstream, int from, int to) =>
        new(AttentionKind.Attended, workstream, At(from), At(to));

    private static AttentionInterval Machine(string workstream, int from, int to) =>
        new(AttentionKind.Machine, workstream, At(from), At(to));

    private static DateTimeOffset At(int seconds) => Zero.AddSeconds(seconds);
}
