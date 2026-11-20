using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectCards;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Which cards leave the grid (project-view AC 13 and 22; 1.1c AC 1): those a readable project's file names, looked up
/// by the card, not read off it, so a card a refresh has just rebuilt with no project on it still leaves; with no card in
/// any project no rule applies at all.
/// </summary>
[TestFixture]
public class ProjectGridRuleTests
{
    private static readonly WorkstreamCardViewModel Member = new(Card("member"));
    private static readonly WorkstreamCardViewModel AdHoc = new(Card("ad-hoc"));

    private static ProjectMember? Named(WorkstreamCardViewModel card) =>
        card.Key == "member" ? new ProjectMember("p", "a-slug") : null;

    [Test]
    public void NoCardInAnyProject_KeepsEveryCardInTheGrid() =>
        Assert.That(ProjectGridRule.KeepOutOfGrid(_ => null, anyMembers: false), Is.Null);

    [Test]
    public void ACardAProjectNames_LeavesTheGrid_EvenWithNoProjectOnIt_AndAdHocCardsStay()
    {
        var rule = ProjectGridRule.KeepOutOfGrid(Named, anyMembers: true)!;

        Assert.That(Member.Workstream.Project, Is.Null, "as a refresh rebuilds it");
        Assert.That((rule(Member), rule(AdHoc)), Is.EqualTo((true, false)));
    }
}
