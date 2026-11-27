using Theseus.Core.Attention;
using Theseus.Core.Running;

namespace Theseus.Tests.Attention;

/// <summary>7.3: the ledger's headless runs are merged into machine time with the overlap the sampler already saw removed.</summary>
[TestFixture]
public class MachineTimeTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void Merge_CountsARunTheSamplerNeverSaw_InFull()
    {
        var merged = MachineTime.Merge([], [Run("verify-a", "s9", 0, 30)]);

        Assert.That(merged.Single(), Is.EqualTo(new AttentionInterval(AttentionKind.Machine, "verify-a", At(0), At(30))
        {
            Session = "s9",
            Project = "theseus",
            Source = MachineTime.LedgerSource,
        }));
    }

    [Test]
    public void Merge_RemovesTheOverlapWithTheSamplesOfTheSameSession()
    {
        var sampled = new AttentionInterval(AttentionKind.Machine, "verify-a", At(10), At(20)) { Session = "s9" };

        var merged = MachineTime.Merge([sampled], [Run("verify-a", "s9", 0, 30)]);

        Assert.That(Total(merged), Is.EqualTo(TimeSpan.FromMinutes(30)), "the ten sampled minutes are not counted twice");
        Assert.That(merged.Count(interval => interval.Source == MachineTime.LedgerSource), Is.EqualTo(2));
    }

    [Test]
    public void Merge_KeepsTheRunWhereAnotherSessionOfTheCardWasSampled()
    {
        var tab = new AttentionInterval(AttentionKind.Machine, "alpha", At(0), At(30)) { Session = "tab" };

        var merged = MachineTime.Merge([tab], [Run("alpha", "headless", 0, 30)]);

        Assert.That(Total(merged), Is.EqualTo(TimeSpan.FromMinutes(60)), "two sessions busy at once are two sessions' work");
    }

    [Test]
    public void Merge_RemovesTheOverlapWithALineFromBeforeSessionsWereStamped_OnTheSameCard()
    {
        var old = new AttentionInterval(AttentionKind.Machine, "alpha", At(0), At(20));

        var merged = MachineTime.Merge([old], [Run("alpha", "headless", 10, 30)]);

        Assert.That(Total(merged), Is.EqualTo(TimeSpan.FromMinutes(30)));
    }

    [Test]
    public void Merge_LeavesAttendedTimeAlone()
    {
        var attended = new AttentionInterval(AttentionKind.Attended, "alpha", At(0), At(30)) { Session = "headless" };

        var merged = MachineTime.Merge([attended], [Run("alpha", "headless", 0, 30)]);

        Assert.That(merged.Where(interval => interval.Kind == AttentionKind.Machine).Sum(interval => interval.Length.TotalMinutes), Is.EqualTo(30));
    }

    private static TimeSpan Total(IEnumerable<AttentionInterval> intervals) =>
        intervals.Where(interval => interval.Kind == AttentionKind.Machine).Aggregate(TimeSpan.Zero, (sum, interval) => sum + interval.Length);

    private static RunRecord Run(string card, string session, int from, int to) =>
        new(Guid.NewGuid(), card, "theseus", session, RunReason.Owner, At(from), At(to), RunOutcome.Completed, 0.1m, RunTokens.None);

    private static DateTimeOffset At(int minutes) => Nine.AddMinutes(minutes);
}
