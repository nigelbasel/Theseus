using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ScaleFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// On a project of 35 objectives, the sentence, the band's heading, the "waiting on you" chip, the project card's chip and
/// the dialog's rows are one count, from ProjectOverview.Waits (project-view-scale, 2026-11-24).
/// </summary>
[TestFixture]
public class LargeProjectWaitsCountTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void TheSentence_Heading_Chip_CardChip_AndDialog_GiveOneCount()
    {
        var view = new ProjectViews(_folder).Large();
        var overview = view.Overview;
        var card = new ProjectCardViewModel(ScaleFixtures.Large, "Large");
        ProjectCardFace.Fill(card, [], overview: overview);
        var count = overview.Waits.Count;

        Assert.That(count, Is.EqualTo(Yours));
        Assert.That(view.Sentence, Does.Contain("Seven things wait for you"));
        Assert.That(view.WaitsHeading, Is.EqualTo($"◆ Waits for you ({count})"));
        Assert.That(view.Counts.Single(chip => chip.State == StageNodeState.WaitingOnYou).Count, Is.EqualTo(count));
        Assert.That(card.Counts.Single(chip => chip.State == StageNodeState.WaitingOnYou).Count, Is.EqualTo(count));
        Assert.That(view.Checklist([]).Groups.Count(group => group.Whom == "For you"), Is.EqualTo(count));
    }
}
