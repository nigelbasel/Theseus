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
}
