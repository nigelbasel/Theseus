using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectCards;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Which cards leave the grid, from what was read (project-view AC 13 and 22): a readable project's do; an unreadable,
/// pinned or unlisted project's stay; with nothing readable no rule applies at all.
/// </summary>
[TestFixture]
public class ProjectGridRuleTests
{
    private static readonly ProjectFileReading Readable = ProjectFileReader.Read("p",
        "# P\n\n**Kit:** method-v0.2\n**Primary branch:** owner/p\n");

    private static readonly WorkstreamCardViewModel Member = new(Card("member", "p", "a-slug"));
    private static readonly WorkstreamCardViewModel AdHoc = new(Card("ad-hoc"));

    [Test]
    public void NothingRead_OrNothingReadable_KeepsEveryCardInTheGrid()
    {
        Assert.That(ProjectGridRule.KeepOutOfGrid([]), Is.Null);
        Assert.That(ProjectGridRule.KeepOutOfGrid([ProjectFileReading.Unreadable("p", "line 1: no **Kit:** line under the title"),
            ProjectFileReading.PinnedTo("q")]), Is.Null);
    }

    [Test]
    public void AReadableProjectsCards_LeaveTheGrid_AndAdHocCardsStay()
    {
        var rule = ProjectGridRule.KeepOutOfGrid([Readable])!;

        Assert.That((rule(Member), rule(AdHoc)), Is.EqualTo((true, false)));
    }

    [Test]
    public void AProjectThatStopsReading_LetsItsCardsBack()
    {
        var rule = ProjectGridRule.KeepOutOfGrid([Readable, ProjectFileReading.Unreadable("q", "line 3: broken")])!;
        var other = new WorkstreamCardViewModel(Card("other", "q", "b-slug"));

        Assert.That((rule(Member), rule(other)), Is.EqualTo((true, false)));
    }
}
