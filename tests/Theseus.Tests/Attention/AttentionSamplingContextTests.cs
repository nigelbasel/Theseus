using Theseus.Core.Attention;
using Theseus.Core.Domain;
using Theseus.Core.Sessions;

namespace Theseus.Tests.Attention;

/// <summary>7.3: Theseus's own windows count as attended time, and every busy session the run log knows counts as machine time.</summary>
[TestFixture]
public class AttentionSamplingContextTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 13, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Sast = TimeZoneInfo.CreateCustomTimeZone("SAST", TimeSpan.FromHours(2), "SAST", "SAST");

    [Test]
    public void Sample_GivesTimeAtACardsWindowInTheseusToThatCard()
    {
        var sample = Sample(terminalInFront: false, theseus: new TheseusFocus("alpha", null));

        Assert.That((sample.Attending, sample.AttendingSource, sample.AttendingSession), Is.EqualTo(("alpha", "theseus", "s-last")));
    }

    [Test]
    public void Sample_GivesTimeAtAProjectsViewToTheProject()
    {
        var sample = Sample(terminalInFront: false, theseus: new TheseusFocus(null, "theseus"));

        Assert.That(sample.Attending, Is.EqualTo(AttentionSampling.ProjectPrefix + "theseus"));
        Assert.That(sample.AttendingStamp, Is.EqualTo(new AttentionStamp("theseus", null, null)));
    }

    [Test]
    public void Sample_KeepsTimeAtTheseusWithNothingInView_AsUntracked()
    {
        var sample = Sample(terminalInFront: false, theseus: new TheseusFocus(null, null));

        Assert.That(sample.Attending, Is.EqualTo(AttentionSampling.UnmatchedPrefix + AttentionSampling.TheseusName));
    }

    [Test]
    public void Sample_AttendsNothingWhenNeitherTheTerminalNorTheseusIsInFront()
    {
        var sample = Sample(terminalInFront: false, theseus: null);

        Assert.That(sample.Attending, Is.Null);
    }

    [Test]
    public void Sample_TakesTheTerminalOverTheseus_AndSaysWhereItWasSeen()
    {
        var sample = Sample(terminalInFront: true, theseus: new TheseusFocus("beta", null), tab: "Alpha");

        Assert.That((sample.Attending, sample.AttendingSource), Is.EqualTo(("alpha", "terminal")));
    }

    [Test]
    public void Sample_CountsABusySessionTheRunLogGivesToACard_ThoughItIsNotTheCardsLast()
    {
        var sample = Sample(terminalInFront: false, theseus: null, live: [Live("s-verifier-run"), Live("s-last"), Live("s-stranger")]);

        Assert.That(sample.BusySessions.Select(session => (session.Workstream, session.SessionId)),
            Is.EquivalentTo(new[] { ("alpha", (string?)"s-verifier-run"), ("alpha", (string?)"s-last") }));
        Assert.That(sample.Busy, Is.EqualTo(new[] { "alpha" }));
    }

    [Test]
    public void Sample_StampsTheCardsPieceAndTheOffsetInForce()
    {
        var sample = Sample(terminalInFront: false, theseus: new TheseusFocus("alpha", null), live: [Live("s-last")]);

        Assert.That(sample.Offset, Is.EqualTo("+02:00"));
        Assert.That(sample.AttendingStamp, Is.EqualTo(new AttentionStamp("theseus", "piece-a", "Implement")));
        Assert.That(sample.BusySessions.Single().Stamp, Is.EqualTo(new AttentionStamp("theseus", "piece-a", "Implement")));
    }

    private static AttentionSample Sample(bool terminalInFront, TheseusFocus? theseus, string? tab = null, IReadOnlyList<LiveSession>? live = null)
    {
        var context = new AttentionContext(theseus,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["s-verifier-run"] = "alpha", ["s-last"] = "alpha" },
            card => card.Key == "alpha" ? new AttentionStamp("theseus", "piece-a", "Implement") : null,
            Sast);

        return AttentionSampling.Sample(Now, terminalInFront, tab, [Card("alpha", "Alpha", "s-last"), Card("beta", "Beta", null)], live ?? [], context);
    }

    private static Workstream Card(string key, string title, string? sessionId) =>
        new(key, title, "#208DD7", ColorIsManual: true, @"C:\work", [], "Prompt.", 1, "New",
            IsBlocked: false, ReopenTomorrow: true, IsArchived: false, sessionId, [], null, DateTimeOffset.UtcNow);

    private static LiveSession Live(string sessionId) => new(1234, sessionId, @"C:\work", "name", "busy", "cli", DateTimeOffset.UtcNow);
}
