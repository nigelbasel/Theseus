using Theseus.Core.Attention;

namespace Theseus.Tests.Attention;

/// <summary>7.3: what is open is written every minute, so a crash loses at most that, and the stretch not measured is kept.</summary>
[TestFixture]
public class AttentionCheckpointTests
{
    private static readonly DateTimeOffset Zero = new(2026, 11, 27, 13, 0, 0, TimeSpan.Zero);

    private string _folder = null!;
    private string _path = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"theseus-checkpoint-{Guid.NewGuid():N}");
        _path = Path.Combine(_folder, "attention-open.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Test]
    public void Recover_AfterACrash_GivesBackWhatWasOpenAndTheTimeNotMeasured_Once()
    {
        var open = new[] { new AttentionInterval(AttentionKind.Machine, "alpha", Zero, Zero.AddSeconds(50)) { Session = "s1" } };
        new AttentionCheckpoint(_path).Write(open, Zero.AddSeconds(55));

        var restarted = new AttentionCheckpoint(_path);
        var (intervals, gap) = restarted.Recover(Zero.AddMinutes(10), "+02:00");

        Assert.That(intervals, Is.EqualTo(open));
        Assert.That(gap, Is.EqualTo(new AttentionGap(Zero.AddSeconds(55), Zero.AddMinutes(10), AttentionGap.Crashed) { Offset = "+02:00" }));
        Assert.That(new AttentionCheckpoint(_path).Recover(Zero.AddMinutes(11), null).Intervals, Is.Empty,
            "a second start after the first must not write the same intervals again");
    }

    [Test]
    public void Recover_AfterACleanClose_GivesNoIntervals_AndSaysTheseusWasNotRunning()
    {
        new AttentionCheckpoint(_path).Close(Zero);

        var (intervals, gap) = new AttentionCheckpoint(_path).Recover(Zero.AddHours(9), null);

        Assert.That(intervals, Is.Empty);
        Assert.That(gap?.Reason, Is.EqualTo(AttentionGap.NotRunning));
        Assert.That(gap?.Length, Is.EqualTo(TimeSpan.FromHours(9)));
    }

    [Test]
    public void Recover_WithNoCheckpoint_GivesNothing()
    {
        var (intervals, gap) = new AttentionCheckpoint(_path).Recover(Zero, null);

        Assert.That(intervals, Is.Empty);
        Assert.That(gap, Is.Null);
    }

    [Test]
    public void IsDue_EveryMinute()
    {
        var checkpoint = new AttentionCheckpoint(_path);

        Assert.That(checkpoint.IsDue(Zero), Is.True, "the first is due at once");
        checkpoint.Write([], Zero);
        Assert.That(checkpoint.IsDue(Zero.AddSeconds(55)), Is.False);
        Assert.That(checkpoint.IsDue(Zero.AddSeconds(60)), Is.True);
    }

    [Test]
    public void ARecorderCheckpointedThenCrashed_LosesAtMostTheMinuteSinceTheCheckpoint()
    {
        var recorder = new AttentionRecorder(TimeSpan.FromSeconds(20));
        var checkpoint = new AttentionCheckpoint(_path);

        for (var seconds = 0; seconds <= 90; seconds += 5)
        {
            recorder.Observe(new AttentionSample(Zero.AddSeconds(seconds), "alpha", []));

            if (checkpoint.IsDue(Zero.AddSeconds(seconds)))
            {
                checkpoint.Write(recorder.OpenIntervals(), Zero.AddSeconds(seconds));
            }
        }

        // The process dies here, with nothing flushed.
        var recovered = new AttentionCheckpoint(_path).Recover(Zero.AddMinutes(5), null).Intervals.Single();

        Assert.That(recovered.Length, Is.EqualTo(TimeSpan.FromSeconds(60)), "written at 60 s, lost the 30 s after");
    }
}
