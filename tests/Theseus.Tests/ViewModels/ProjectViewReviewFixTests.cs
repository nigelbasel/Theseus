using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectCards;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The project view and card after the review of PR 2: a new commit is not a new view, its source updates in place
/// (item 4); a project card says when its last read failed (item 9); and a project card moves only where cards do
/// (item 7).
/// </summary>
[TestFixture]
public class ProjectViewReviewFixTests
{
    private static ProjectFileReading Reading(string commit) =>
        ProjectFileReader.Read("p", """
            # P

            **Kit:** method-v0.2
            **Primary branch:** owner/p

            ## Phase 0: Seed

            ### Objective 0.1: First
            - [ ] One.

            **Pipeline:** feature
            | Workstream | Slug | Branch | Repos |
            |---|---|---|---|
            | Work | a-slug | owner/a-slug | Repo |
            | Other | b-slug | owner/b-slug | Repo |
            """) with
        {
            Artefacts = new Dictionary<string, WorkstreamArtefacts>
            {
                ["a-slug"] = new("a-slug", new WorkstreamSource("owner/a-slug", commit, false), new HashSet<string> { "research.md" }),
                ["b-slug"] = new("b-slug", new WorkstreamSource("owner/b-slug", commit, false), new HashSet<string> { "research.md" }),
            },
        };

    private static ProjectViewModel Build(string commit, IReadOnlyList<WorkstreamCardViewModel>? cards = null) =>
        ProjectViewModel.Build(Reading(commit), cards ?? [], (_, _) => null, (Workstream _) => false, _ => { }, _ => { }, null);

    [Test]
    public void ANewCommit_KeepsTheSignature_SoTheViewIsNotRebuilt()
    {
        Assert.That(Build("2222222222222").Signature, Is.EqualTo(Build("1111111111111").Signature));
    }

    [Test]
    public void ANewCommit_UpdatesWhereEachSlugWasRead_InPlace()
    {
        var shown = Build("1111111111111");
        var entry = shown.Rows[0].Cells[0].Entries.Single(each => each.Slug == "b-slug");

        shown.TakeSourcesFrom(Build("2222222222222"));

        Assert.That(entry.Source, Is.EqualTo("read from owner/b-slug at 2222222"));
        Assert.That(entry.ToolTipText, Does.EndWith("read from owner/b-slug at 2222222"));
    }

    [Test]
    public void AProjectCard_SaysWhenItsLastReadFailed()
    {
        var card = new ProjectCardViewModel("p", "P");

        Assert.That(card.ToolTipText, Is.EqualTo("P"));

        card.LastFailure = "git did not answer within 10 s";

        Assert.That(card.ToolTipText, Does.Contain("The last read failed: git did not answer within 10 s"));
    }

    [Test]
    public void AProjectCard_IsNotMoved_WhereCardsCannotBeDropped()
    {
        var fixture = new BoardFixture().With("ad-hoc");
        fixture.Board.Refresh();
        var project = new ProjectCardViewModel("p", "P");
        fixture.Board.ShowProjects([project]);
        fixture.Board.ColumnsBySessionState = true;

        fixture.Board.MoveProject("p", "In progress", "Dev");

        Assert.That(fixture.Settings.ProjectCardPlaces, Is.Empty);
    }
}
