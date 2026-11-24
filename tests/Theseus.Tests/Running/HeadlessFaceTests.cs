using Theseus.Core.Running;
using static Theseus.Tests.Running.RunRecords;

namespace Theseus.Tests.Running;

[TestFixture]
public class HeadlessFaceTests
{
    private static string? Face(HeadlessCardState? state) => HeadlessFace.For(state, Noon, TimeZoneInfo.Utc);

    [Test]
    public void ARunGoing_ShowsHowLongAndItsTokens()
    {
        var state = new HeadlessCardState(true, null, Noon.AddMinutes(-12), "Bash: ls", new RunTokens(Output: 431_000), null, null, null);

        Assert.That(Face(state), Is.EqualTo("Headless · running 12m · 431k tokens"));
    }

    [Test]
    public void AQueuedCard_ShowsItsPlace()
    {
        Assert.That(Face(new HeadlessCardState(false, 2, null, null, RunTokens.None, null, null, null)), Is.EqualTo("Headless · queued (2nd)"));
    }

    [Test]
    public void AnEndedRun_ShowsWhenItsCostAndTokens()
    {
        var last = Run(cost: 1.42m, ended: Noon.AddHours(-1)) with { Tokens = new RunTokens(Output: 2_200_000) };

        Assert.That(Face(new HeadlessCardState(false, null, null, null, last.Tokens, 1.42m, last, null)),
            Is.EqualTo("Headless · ended 11:00 · $1.42 · 2.2M tokens"));
    }

    [Test]
    public void ARunStoppedAtALimit_SaysWhichAndByHowMuch()
    {
        var last = Run(cost: 5.03m, outcome: RunOutcome.StoppedAtLimit) with { LimitDetail = "Stopped at its limit: run cost $5.03 of $5.00." };

        Assert.That(Face(new HeadlessCardState(false, null, null, null, last.Tokens, 5.03m, last, null)),
            Does.EndWith("Stopped at its limit: run cost $5.03 of $5.00."));
    }

    [Test]
    public void ACardWithNoHeadlessRun_ShowsNothing()
    {
        Assert.That(Face(null), Is.Null);
    }
}
