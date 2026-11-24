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
        var reading = WriteLarge(_folder);
        var overview = ProjectOverview.For(reading.File!, [], [], [], 0);
        var view = ProjectViewModel.Build(reading, [], ProjectViewFixtures.LinksOn([], []), [], new MemorySettingsStore(),
            new ProjectViews(_folder).Actions, overview: overview);
        var card = new ProjectCardViewModel(reading.Name, reading.File!.Title);
        ProjectCardFace.Fill(card, [], overview: overview);
        var count = overview.Waits.Count;

        Assert.That(count, Is.EqualTo(Objectives + 1));
        Assert.That(view.Sentence, Does.Contain($"{count} things wait for you"));
        Assert.That(view.WaitsHeading, Is.EqualTo($"◆ Waits for you ({count})"));
        Assert.That(view.Counts.Single(chip => chip.State == StageNodeState.WaitingOnYou).Count, Is.EqualTo(count));
        Assert.That(card.Counts.Single(chip => chip.State == StageNodeState.WaitingOnYou).Count, Is.EqualTo(count));
        Assert.That(view.AllWaits.Rows, Has.Count.EqualTo(count));
    }
}
