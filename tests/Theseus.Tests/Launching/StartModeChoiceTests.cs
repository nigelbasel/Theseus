using Theseus.Core.Domain;
using Theseus.Core.Launching;

namespace Theseus.Tests.Launching;

[TestFixture]
public class StartModeChoiceTests
{
    private const string ResumeCost = "Resume this session. It works through most of a 30 MB transcript.";

    [Test]
    public void For_ResumesWhenAskedAndThereIsAConversationToResume()
    {
        Assert.That(StartModeChoice.For(StartMode.Resume, transcriptExists: true), Is.EqualTo(LaunchMode.Resume));
    }

    [Test]
    public void For_StartsANewSessionWhenAskedToStartFreshEvenThoughItCouldResume()
    {
        Assert.That(StartModeChoice.For(StartMode.Fresh, transcriptExists: true), Is.EqualTo(LaunchMode.FirstRun));
    }

    [TestCase(StartMode.Resume)]
    [TestCase(StartMode.Fresh)]
    public void For_StartsANewSessionWhenThereIsNothingToResume(StartMode requested)
    {
        Assert.That(StartModeChoice.For(requested, transcriptExists: false), Is.EqualTo(LaunchMode.FirstRun));
    }

    [Test]
    public void Tooltip_GivesTheCostOfResumingAndOffersAFreshStartWhenResumeIsTheDefault()
    {
        var tooltip = StartModeChoice.Tooltip(StartMode.Resume, transcriptExists: true, ResumeCost);

        Assert.That(tooltip, Does.StartWith(ResumeCost));
        Assert.That(tooltip, Does.Contain("start fresh"));
    }

    [Test]
    public void Tooltip_SaysAFreshStartReloadsNothingAndOffersResumeWhenFreshIsTheDefault()
    {
        var tooltip = StartModeChoice.Tooltip(StartMode.Fresh, transcriptExists: true, ResumeCost);

        Assert.That(tooltip, Does.StartWith("Start a fresh session"));
        Assert.That(tooltip, Does.Contain("resume"));
        Assert.That(tooltip, Does.Not.Contain(ResumeCost));
    }

    [TestCase(StartMode.Resume)]
    [TestCase(StartMode.Fresh)]
    public void Tooltip_OffersNoChoiceWhenThereIsNoConversationToResume(StartMode preferred)
    {
        var tooltip = StartModeChoice.Tooltip(preferred, transcriptExists: false, ResumeCost);

        Assert.That(tooltip, Does.Contain("no earlier conversation"));
        Assert.That(tooltip, Does.Not.Contain("▾"));
    }

    // Start again on a verifier's Waits line and its build's box asks for the board's default, Resume, and every verifier
    // that had run had a conversation on disk: each was resumed, ended at once and filed nothing (Nigel, 2026-11-25).
    [Test]
    public void For_AVerifierStartedAgainWithAConversationOnDisk_StartsAFreshRun()
    {
        Assert.That(StartModeChoice.For(StartMode.Resume, transcriptExists: true, WorkstreamRole.Verifier), Is.EqualTo(LaunchMode.FirstRun));
    }

    [Test]
    public void For_AVerifierStartedFresh_StartsAFreshRun()
    {
        Assert.That(StartModeChoice.For(StartMode.Fresh, transcriptExists: true, WorkstreamRole.Verifier), Is.EqualTo(LaunchMode.FirstRun));
    }

    [Test]
    public void For_AVerifierTheOwnerChoseToResumeFromItsCardMenu_IsResumed()
    {
        Assert.That(StartModeChoice.For(StartMode.Resume, transcriptExists: true, WorkstreamRole.Verifier, chosenOnTheCard: true),
            Is.EqualTo(LaunchMode.Resume));
    }

    // A scenario writer is a one-shot run as a verifier is: resumed, both projects' writers ended in seconds with no
    // progress, seven runs for $91.96, until their breakers tripped and no build was checked (2026-11-25).
    [TestCase(StartMode.Resume)]
    [TestCase(StartMode.Fresh)]
    public void For_AScenarioWriterStartedFromAnyPath_StartsAFreshRun(StartMode requested)
    {
        Assert.That(StartModeChoice.For(requested, transcriptExists: true, WorkstreamRole.ScenarioWriter), Is.EqualTo(LaunchMode.FirstRun));
    }

    [Test]
    public void For_AScenarioWriterTheOwnerChoseToResumeFromItsCardMenu_IsResumed()
    {
        Assert.That(StartModeChoice.For(StartMode.Resume, transcriptExists: true, WorkstreamRole.ScenarioWriter, chosenOnTheCard: true),
            Is.EqualTo(LaunchMode.Resume));
    }

    [TestCase(StartMode.Resume)]
    [TestCase(StartMode.Fresh)]
    public void Tooltip_OnAScenarioWritersOwnStart_SaysItStartsAFreshRunWithItsOwnPrompt(StartMode preferred)
    {
        var tooltip = StartModeChoice.Tooltip(preferred, transcriptExists: true, ResumeCost, WorkstreamRole.ScenarioWriter);

        Assert.That(tooltip, Is.EqualTo(StartModeChoice.ScenarioWriterTooltip));
        Assert.That(tooltip, Does.Contain("fresh").And.Contain("own prompt"));
    }

    [TestCase(WorkstreamRole.None)]
    [TestCase(WorkstreamRole.ProjectUpdater)]
    public void For_AnyOtherCardAskedToResume_IsStillResumed(WorkstreamRole role)
    {
        Assert.That(StartModeChoice.For(StartMode.Resume, transcriptExists: true, role), Is.EqualTo(LaunchMode.Resume));
    }

    [Test]
    public void Tooltip_ForAVerifier_SaysItStartsAFreshVerificationRun()
    {
        Assert.That(StartModeChoice.VerifierTooltip, Does.Contain("fresh verification run"));
    }

    [TestCase(StartMode.Resume)]
    [TestCase(StartMode.Fresh)]
    public void Tooltip_OnAVerifiersOwnStart_SaysItStartsAFreshVerificationRun_NotAResume(StartMode preferred)
    {
        var tooltip = StartModeChoice.Tooltip(preferred, transcriptExists: true, ResumeCost, WorkstreamRole.Verifier);

        Assert.That(tooltip, Is.EqualTo(StartModeChoice.VerifierTooltip));
    }
}
