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

    [TestCase(false)]
    [TestCase(true)]
    public void For_GivesAWorkstreamsOwnInstructionsOnEveryStart(bool hasHistory)
    {
        Assert.That(WithOwnInstructions("Run the tests before committing.", hasHistory),
            Does.Contain("Run the tests before committing."));
    }

    [Test]
    public void For_PutsAWorkstreamsOwnInstructionsAfterEverythingTheBoardSays()
    {
        var first = WithOwnInstructions("Own words.", hasHistory: false, preamble: "House rules.");
        var returning = WithOwnInstructions("Own words.", hasHistory: true, preamble: "House rules.");

        Assert.That(first.IndexOf("Own words.", StringComparison.Ordinal),
            Is.GreaterThan(first.IndexOf(Original, StringComparison.Ordinal)));
        Assert.That(returning.IndexOf("Own words.", StringComparison.Ordinal),
            Is.GreaterThan(returning.IndexOf("Do not start again", StringComparison.Ordinal)));
        Assert.That(returning.IndexOf("Own words.", StringComparison.Ordinal),
            Is.GreaterThan(returning.IndexOf("House rules.", StringComparison.Ordinal)));
    }

    [Test]
    public void For_FillsInTheHandoffInAWorkstreamsOwnInstructions()
    {
        var opening = WithOwnInstructions("Also skim " + SessionOpening.HandoffPlaceholder + ".", hasHistory: true);

        Assert.That(opening, Does.Contain($"Also skim {Handoff}."));
    }

    [Test]
    public void For_AddsNothingForAWorkstreamWithNoInstructionsOfItsOwn()
    {
        Assert.That(WithOwnInstructions("   ", hasHistory: true), Is.EqualTo(For(hasHistory: true)));
    }

    [Test]
    public void For_OpensWithTheIdentityWordingFromSettings()
    {
        var opening = SessionOpening.For(null, null, Workstream("session-1"), hasHistory: true, Handoff,
            handoffExists: true, identityTemplate: "I am {key}.");

        Assert.That(opening, Does.StartWith("I am contact-forms."));
    }

    private static readonly DateTimeOffset ResumedAt = new(2026, 9, 30, 8, 2, 0, TimeSpan.Zero);

    [Test]
    public void ForResume_SaysWhenTheSessionWasResumedAndNothingTheSessionAlreadyKnows()
    {
        var opening = SessionOpening.ForResume(Workstream("session-1"), ResumedAt, Handoff);
        var local = ResumedAt.ToLocalTime();

        Assert.That(opening, Does.StartWith($"{SessionOpening.ResumedPrefix}{local:HH:mm} on {local:yyyy-MM-dd}."));
        Assert.That(opening, Does.Not.Contain("You are the"), "a resumed conversation knows which workstream it is");
        Assert.That(opening, Does.Not.Contain(Original));
        Assert.That(opening, Does.Not.Contain("Do not start again"),
            "the continuation tells a session to read its notes, which a busy resumed session must not be told");
        Assert.That(opening, Does.Not.Contain(Handoff));
    }

    [Test]
    public void ForResume_StillGivesAWorkstreamItsOwnEveryStartInstructions()
    {
        var workstream = Workstream("session-1") with { EveryStartPrompt = "Also skim " + SessionOpening.HandoffPlaceholder + "." };

        var opening = SessionOpening.ForResume(workstream, ResumedAt, Handoff);

        Assert.That(opening, Does.EndWith($"Also skim {Handoff}."));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void For_GivesTheRelatedWorkstreamsBeforeTheWorkstreamsOwnInstructions(bool hasHistory)
    {
        var workstream = Workstream("session-1") with { EveryStartPrompt = "Own instructions." };

        var opening = SessionOpening.For(null, null, workstream, hasHistory, Handoff, handoffExists: true,
            related: "Related workstreams (from the Theseus board):");

        Assert.That(opening, Does.Contain("Related workstreams"));
        Assert.That(opening.IndexOf("Related workstreams", StringComparison.Ordinal),
            Is.LessThan(opening.IndexOf("Own instructions.", StringComparison.Ordinal)));
    }

    [Test]
    public void ForResume_GivesWhatChangedInItsLinks()
    {
        var opening = SessionOpening.ForResume(Workstream("session-1"), ResumedAt, Handoff,
            related: "- Since your last session: Nigel accepted the output of \"Token spike\".");

        Assert.That(opening, Does.Contain("Since your last session"));
    }

    [Test]
    public void ForResume_IsOneLineForAWorkstreamWithNoInstructionsOfItsOwn()
    {
        var opening = SessionOpening.ForResume(Workstream("session-1") with { EveryStartPrompt = "  " }, ResumedAt, Handoff);

        Assert.That(opening, Does.Not.Contain(Environment.NewLine));
        Assert.That(opening, Does.EndWith("carry on where you left off."));
    }

    private static string WithOwnInstructions(string everyStart, bool hasHistory, string? preamble = null) =>
        SessionOpening.For(preamble, null, Workstream("session-1") with { EveryStartPrompt = everyStart },
            hasHistory, Handoff, handoffExists: true);
}
