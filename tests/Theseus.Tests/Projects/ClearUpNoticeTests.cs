using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// The line a clear-up leaves (Nigel, 2026-11-23: how long does it stay?): a notice, not a wait, saying when Undo ends; it
/// lapses after 24 hours, or when the project's next round starts, and with it the means to undo.
/// </summary>
[TestFixture]
public class ClearUpNoticeTests
{
    private static readonly TimeSpan Here = TimeSpan.FromHours(2);
    private static readonly DateTimeOffset At = new(2026, 11, 23, 19, 24, 0, Here);

    private static VerifierClearUpRecord Record(int cards = 7, int round = 2) =>
        new("theseus", [.. Enumerable.Range(1, cards).Select(index => $"card-{index}")], [], []) { AtUtc = At.ToUniversalTime(), Round = round };

    [Test]
    public void ItSaysWhatWasDone_AndWhenUndoEnds()
    {
        Assert.That(ClearUpNotice.For(Record(), At.AddMinutes(5), currentRound: 2),
            Is.EqualTo("Seven cards were cleared up: stopped, archived, their reports rejected. Undo until 19:24 tomorrow."));
        Assert.That(ClearUpNotice.For(Record(cards: 1), At.AddHours(5), currentRound: 2),
            Is.EqualTo("One card was cleared up: stopped, archived, its reports rejected. Undo until 19:24 today."),
            "said from the next morning's point of view, it ends today");
    }

    [Test]
    public void ItLapsesAfterADay()
    {
        Assert.That(ClearUpNotice.For(Record(), At.AddHours(24), currentRound: 2), Is.Null);
    }

    [Test]
    public void ItLapsesWhenTheProjectsNextRoundStarts()
    {
        Assert.That(ClearUpNotice.For(Record(round: 2), At.AddMinutes(5), currentRound: 3), Is.Null);
    }
}
