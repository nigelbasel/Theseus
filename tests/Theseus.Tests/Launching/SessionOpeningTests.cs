using Theseus.Core.Domain;
using Theseus.Core.Launching;

namespace Theseus.Tests.Launching;

[TestFixture]
public class SessionOpeningTests
{
    private const string Original = "Pick up the contact form work and add validation.";
    private const string Handoff = @"C:\work\repo\HANDOFF-contact-forms.md";

    [Test]
    public void For_GivesAWorkstreamItsOwnPromptTheFirstTime()
    {
        Assert.That(For(hasHistory: false), Does.Contain(Original));
    }

    [Test]
    public void For_DoesNotReplayTheOpeningPromptOnceTheWorkHasBegun()
    {
        Assert.That(For(hasHistory: true), Does.Not.Contain(Original));
    }

    [Test]
    public void For_PointsAReturningSessionAtItsHandoff()
    {
        Assert.That(For(hasHistory: true), Does.Contain(Handoff));
    }

    [Test]
    public void For_TellsAReturningSessionNotToStartAgain()
    {
        Assert.That(For(hasHistory: true), Does.Contain("Do not start again"));
    }

    [Test]
    public void For_UsesTheConfiguredContinuationWhenThereIsOne()
    {
        var opening = For(hasHistory: true,
            continuePrompt: "Resume. Notes are in " + SessionOpening.HandoffPlaceholder + ".");

        Assert.That(opening, Does.Contain($"Resume. Notes are in {Handoff}."));
    }

    [Test]
    public void For_LeavesNoPlaceholderUnfilled()
    {
        Assert.That(For(hasHistory: true), Does.Not.Contain(SessionOpening.HandoffPlaceholder));
    }

    [Test]
    public void For_KeepsTheStandingPreambleAheadOfEitherKindOfPrompt()
    {
        var first = For(hasHistory: false, preamble: "House rules.");
        var returning = For(hasHistory: true, preamble: "House rules.");

        Assert.That(first.IndexOf("House rules.", StringComparison.Ordinal),
            Is.LessThan(first.IndexOf(Original, StringComparison.Ordinal)));
        Assert.That(returning, Does.Contain("House rules."));
    }

    [Test]
    public void For_OpensBySayingWhichWorkstreamTheSessionIs()
    {
        Assert.That(For(hasHistory: false), Does.StartWith("You are the"),
            "scope cannot be inferred from a directory several workstreams share");
        Assert.That(For(hasHistory: true), Does.StartWith("You are the"));
    }

    [Test]
    public void HasHistory_IsTrueOnceAWorkstreamHasASession()
    {
        Assert.That(SessionOpening.HasHistory(Workstream("session-1"), handoffExists: false), Is.True);
    }

    [Test]
    public void HasHistory_IsTrueWhenAHandoffWasLeftBehindEvenWithNoSessionToResume()
    {
        Assert.That(SessionOpening.HasHistory(Workstream(null), handoffExists: true), Is.True,
            "a session can lose its transcript and still have left notes");
    }

    [Test]
    public void HasHistory_IsFalseForAWorkstreamThatHasNeverRun()
    {
        Assert.That(SessionOpening.HasHistory(Workstream(null), handoffExists: false), Is.False);
    }

    private static string For(bool hasHistory, string? preamble = null, string? continuePrompt = null,
        bool handoffExists = true) =>
        SessionOpening.For(preamble, continuePrompt, Workstream("session-1"), hasHistory, Handoff, handoffExists);

    private static Workstream Workstream(string? sessionId) =>
        new("contact-forms", "Contact form validation", "#208DD7", ColorIsManual: true,
            @"C:\work\repo", [], Original, 1, "Today", IsBlocked: false,
            ReopenTomorrow: true, IsArchived: false, sessionId, [], null, DateTimeOffset.UtcNow);
    [Test]
    public void For_SaysSoWhenTheHandoffItNamesIsNotThere()
    {
        var opening = For(hasHistory: true, handoffExists: false);

        Assert.That(opening, Does.Contain(Handoff));
        Assert.That(opening, Does.Contain("does not exist"));
        Assert.That(opening, Does.Contain("recent commits"));
    }

    [Test]
    public void For_DoesNotSecondGuessAHandoffThatIsThere()
    {
        Assert.That(For(hasHistory: true), Does.Not.Contain("does not exist"));
    }
}
