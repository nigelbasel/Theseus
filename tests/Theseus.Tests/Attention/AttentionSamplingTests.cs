using Theseus.Core.Attention;
using Theseus.Core.Domain;
using Theseus.Core.Sessions;

namespace Theseus.Tests.Attention;

[TestFixture]
public class AttentionSamplingTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 13, 0, 0, TimeSpan.Zero);

    [Test]
    public void Sample_GivesTheFocusedTabToTheWorkstreamItIsNamedAfter()
    {
        var sample = AttentionSampling.Sample(Now, terminalInFront: true, "contact FORM validation",
            [Workstream("contact-forms", "Contact form validation")], []);

        Assert.That(sample.Attending, Is.EqualTo("contact-forms"));
    }

    [Test]
    public void Sample_KeepsATabNoWorkstreamIsNamedAfter()
    {
        var sample = AttentionSampling.Sample(Now, terminalInFront: true, "pwsh", [Workstream("a", "Alpha")], []);

        Assert.That(sample.Attending, Is.EqualTo(AttentionSampling.UnmatchedPrefix + "pwsh"));
    }

    [Test]
    public void Sample_AttendsNothingWhenNoTerminalIsInFront()
    {
        var sample = AttentionSampling.Sample(Now, terminalInFront: false, "Alpha", [Workstream("a", "Alpha")], []);

        Assert.That(sample.Attending, Is.Null);
    }

    [Test]
    public void Sample_CountsTwoBusySessionsOnOneWorkstreamOnce()
    {
        var workstreams = new[] { Workstream("a", "Alpha", "session-1"), Workstream("a", "Alpha copy", "session-2") };

        var sample = AttentionSampling.Sample(Now, false, null, workstreams,
            [Session("session-1", "busy"), Session("session-2", "busy")]);

        Assert.That(sample.Busy, Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public void Sample_CountsOnlyWorkingSessionsThatTheBoardFollows()
    {
        var workstreams = new[] { Workstream("a", "Alpha", "session-1"), Workstream("b", "Beta", "session-2") };

        var sample = AttentionSampling.Sample(Now, false, null, workstreams,
            [Session("session-1", "idle"), Session("session-2", "busy"), Session("stranger", "busy")]);

        Assert.That(sample.Busy, Is.EqualTo(new[] { "b" }));
    }

    private static Workstream Workstream(string key, string title, string? sessionId = null) =>
        new(key, title, "#208DD7", ColorIsManual: true, @"C:\work", [], "Prompt.", 1, "New",
            IsBlocked: false, ReopenTomorrow: true, IsArchived: false, sessionId, [], null, DateTimeOffset.UtcNow);

    private static LiveSession Session(string sessionId, string status) =>
        new(1234, sessionId, @"C:\work", "name", status, "cli", DateTimeOffset.UtcNow);
}
