using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectCards;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The project view (project-view AC 4, 5, 7, 10, 15): rows per objective with workstreams across the seven columns,
/// cards at their stage with the source in their tooltip, slugs with no card at their own stage, the groups at no
/// stage, the verifier beside its subject until it ends, and the spec waiting on the owner with Open and Accept.
/// </summary>
[TestFixture]
public class ProjectViewModelTests
{
    private static readonly WorkstreamSource OnBranch = new("owner/x", "abcdef1234567", false);

    private static ProjectFileReading Reading()
    {
        var reading = ProjectFileReader.Read("p", """
            # P

            **Kit:** method-v0.2
            **Primary branch:** owner/p

            ## Phase 0: Seed
            **Exit gate:** the owner accepts.

            ### Objective 0.1: First
            - [x] One.
            - [ ] Two.

            **Pipeline:** feature
            | Workstream | Slug | Branch | Repos |
            |---|---|---|---|
            | Spec work | old-slug | owner/old-slug | Repo |
            | Spec work | spec-slug | owner/spec-slug | Repo |
            | Done work | done-slug | owner/done-slug | Repo |
            | Idle work | idle-slug | owner/idle-slug | Repo |

            ### Objective 0.2: Nothing yet
            [criteria to be written]

            **Pipeline:** [to be chosen]
            | Workstream | Slug | Branch | Repos |
            |---|---|---|---|
            """);

        return reading with
        {
            Artefacts = new Dictionary<string, WorkstreamArtefacts>
            {
                ["old-slug"] = Files("old-slug", "research.md"),
                ["spec-slug"] = Files("spec-slug", "research.md", "spec.md") with
                {
                    SpecText = "# The spec\n", SpecHash = "spec-now", SpecCommit = "abcdef1234567",
                },
                ["done-slug"] = Files("done-slug", "research.md", "spec.md", "plan.md", "implementation.md") with
                {
                    SpecText = "# Done spec\n", SpecHash = "done-spec", SpecCommit = "1234567abcdef",
                },
                ["idle-slug"] = WorkstreamArtefacts.NotOnMachine("idle-slug") with
                {
                    Source = new WorkstreamSource("owner/p", "fedcba9876543", true),
                },
            },
        };
    }

    private static WorkstreamArtefacts Files(string slug, params string[] files) => new(slug, OnBranch, files.ToHashSet());

    private readonly List<SpecAtGate> _opened = [];
    private readonly List<SpecAtGate> _accepted = [];

    private ProjectViewModel Build(IReadOnlyList<WorkstreamCardViewModel> cards, Func<Workstream, bool>? hasEnded = null) =>
        ProjectViewModel.Build(Reading(), cards,
            (_, slug) => slug == "done-slug" ? new SpecAcceptance("p", slug, "1234567abcdef", "done-spec", DateTimeOffset.UnixEpoch) : null,
            hasEnded ?? (_ => false), _opened.Add, _accepted.Add, null);

    private static WorkstreamCardViewModel Vm(Workstream workstream) => new(workstream);

    private static IReadOnlyList<WorkstreamCardViewModel> Cards() =>
    [
        Vm(Card("card-spec", "p", "spec-slug")),
        Vm(Card("card-done", "p", "done-slug")),
        Vm(Card("checker", "p", "done-slug", WorkstreamRole.Verifier)),
        Vm(Card("fresh", "p")),
        Vm(Card("stray", "p", "gone-slug")),
        Vm(Card("ad-hoc")),
    ];

    private static ProjectStageCellViewModel Cell(ProjectViewModel view, PipelineStage stage) =>
        view.Rows.First(row => !row.IsGroup).Cells.Single(cell => cell.Stage == stage);

    [Test]
    public void ThereIsARowPerObjectiveWithWorkstreams_AcrossTheSevenColumns_ThenTheGroups()
    {
        var view = Build(Cards());

        Assert.That(view.Rows.Select(row => row.Heading), Is.EqualTo(new[] { "0.1 First", "No slug yet", "Not in the project file" }));
        Assert.That(view.Rows[0].Cells.Select(cell => cell.Stage), Is.EqualTo(ProjectViewModel.Stages));
        Assert.That(ProjectViewModel.ColumnHeadings, Has.Count.EqualTo(7));
    }

    [Test]
    public void ACard_SitsAtItsStage_WithWhereItWasReadInItsTooltip()
    {
        var cards = Cards();
        var view = Build(cards);
        var done = cards.Single(card => card.Key == "card-done");

        Assert.That(Cell(view, PipelineStage.Implement).Entries.Select(entry => entry.Card), Does.Contain(done));
        Assert.That(done.FaceTooltip, Is.EqualTo(@"C:\work" + Environment.NewLine + "read from owner/x at abcdef1"));
    }

    [Test]
    public void AnAdHocCard_IsNotInTheView_AndItsTooltipIsUnchanged()
    {
        var cards = Cards();
        var view = Build(cards);
        var adHoc = cards.Single(card => card.Key == "ad-hoc");

        Assert.That(view.CardsInOrder, Does.Not.Contain(adHoc));
        Assert.That(adHoc.FaceTooltip, Is.EqualTo(@"C:\work"));
    }

    [Test]
    public void AReachedSpec_ShowsWaitingOnYou_WithOpenAndAccept()
    {
        var view = Build(Cards());
        var entry = Cell(view, PipelineStage.Spec).Entries.Single(each => each.Slug == "spec-slug");

        Assert.That((entry.IsWaitingOnYou, entry.WaitingText), Is.EqualTo((true, "Waiting on you")));

        entry.OpenSpecCommand!.Execute(null);
        entry.AcceptSpecCommand!.Execute(null);

        Assert.That(_opened.Single().Text, Is.EqualTo("# The spec\n"));
        Assert.That((_accepted.Single().Slug, _accepted.Single().Commit), Is.EqualTo(("spec-slug", "abcdef1234567")));
        Assert.That(view.WaitingSpecs.Select(spec => spec.Slug), Is.EqualTo(new[] { "spec-slug" }));
    }

    [Test]
    public void AnAcceptedSpec_WaitsOnNoOne_AndHasNoAccept()
    {
        var view = Build(Cards());
        var done = Cell(view, PipelineStage.Implement).Entries.Single(each => each.Card?.Key == "card-done");

        Assert.That((done.IsWaitingOnYou, done.AcceptSpecCommand), Is.EqualTo((false, (System.Windows.Input.ICommand?)null)));
        Assert.That(done.Detail, Does.StartWith("Passed: research, spec, plan"));
    }

    [Test]
    public void AnEarlierSlug_ShowsAtItsOwnStage_NotAsACard_AndAnUnstartedSlugSaysSo()
    {
        var view = Build(Cards());
        var old = Cell(view, PipelineStage.Research).Entries.Single(each => each.Slug == "old-slug");
        var idle = Cell(view, PipelineStage.Research).Entries.Single(each => each.Slug == "idle-slug");

        Assert.That((old.HasCard, old.Note), Is.EqualTo((false, "earlier slug")));
        Assert.That((idle.HasCard, idle.Note), Is.EqualTo((false, "not started")));
    }

    [Test]
    public void CardsWithNoSlugOrAnUnlistedSlug_AreInTheirGroups()
    {
        var view = Build(Cards());

        Assert.That(view.Rows.Single(row => row.Heading == "No slug yet").Loose.Select(entry => entry.Card!.Key),
            Is.EqualTo(new[] { "fresh" }));
        Assert.That(view.Rows.Single(row => row.Heading == "Not in the project file").Loose.Single().Note,
            Is.EqualTo("gone-slug is not in the project file"));
    }

    [Test]
    public void AVerifier_SitsInVerify_UntilItEnds()
    {
        Assert.That(Cell(Build(Cards()), PipelineStage.Verify).Entries.Single().Card!.Key, Is.EqualTo("checker"));
        Assert.That(Cell(Build(Cards(), card => card.Key == "checker"), PipelineStage.Verify).Entries, Is.Empty);
    }

    [Test]
    public void TheTree_HasPhasesObjectivesAndSlugs_WithTicksAndWhatWaits()
    {
        var view = Build(Cards());
        var phase = view.Tree.First();
        var objective = phase.Children.First();

        Assert.That((phase.Text, phase.Detail, phase.IsWaitingOnYou), Is.EqualTo(("Phase 0: Seed", "current", true)));
        Assert.That((objective.Text, objective.Detail), Is.EqualTo(("0.1 First", "1 of 2 ticked")));
        Assert.That(objective.Children.Single(slug => slug.Text.StartsWith("spec-slug", StringComparison.Ordinal)).Detail,
            Does.Contain("Waiting on you"));
        Assert.That(objective.Row, Is.SameAs(view.Rows[0]), "selecting it scrolls to its row");
        Assert.That(phase.Children[1].Row, Is.Null, "an objective with no workstreams has no row");
    }

    [Test]
    public void TheCardsInReadingOrder_AreTheViewsCards()
    {
        Assert.That(Build(Cards()).CardsInOrder.Select(card => card.Key),
            Is.EquivalentTo(new[] { "card-spec", "card-done", "checker", "fresh", "stray" }));
    }

    [Test]
    public void TheSameProjectBuiltTwice_HasTheSameSignature()
    {
        var first = Build(Cards()).Signature;
        var second = Build(Cards()).Signature;

        Assert.That(second, Is.EqualTo(first));
    }
}
