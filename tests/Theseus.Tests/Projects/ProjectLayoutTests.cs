using Theseus.Core.Domain;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectCards;

namespace Theseus.Tests.Projects;

/// <summary>
/// Where a project's cards and slugs are laid out (spec AC 4 and 10): cards at their slug, No slug yet, Not in the
/// project file, not started, earlier slugs at their own stage, and the verifier beside its subject until it ends.
/// </summary>
[TestFixture]
public class ProjectLayoutTests
{
    private static readonly ProjectFile File = ProjectFileReader.Read("p", """
        # P

        **Kit:** method-v0.2
        **Primary branch:** owner/p

        ## Phase 0: Seed

        ### Objective 0.1: First
        - [ ] One.

        **Pipeline:** feature
        | Workstream | Slug | Branch | Repos |
        |---|---|---|---|
        | The work | first-slug | owner/first-slug | Repo |
        | The work | second-slug | owner/second-slug | Repo |
        | Other work | other-slug | owner/other-slug | Repo |
        | Unstarted work | waiting-slug | owner/waiting-slug | Repo |
        """).File!;

    private static WorkstreamStage Stage(params PipelineStage[] reached) =>
        new(PipelineKind.Feature, reached, [], reached.Length > 0 ? reached[^1] : null, null, WorkstreamSource.NotOnMachine);

    private static readonly IReadOnlyDictionary<string, WorkstreamStage> Stages = new Dictionary<string, WorkstreamStage>
    {
        ["first-slug"] = Stage(PipelineStage.Research, PipelineStage.Spec),
        ["second-slug"] = Stage(PipelineStage.Research),
        ["other-slug"] = Stage(PipelineStage.Research),
        ["waiting-slug"] = Stage(),
    };

    private static ProjectLayout Lay(IReadOnlyList<Workstream> cards, Func<Workstream, bool>? hasEnded = null) =>
        ProjectLayout.Build(File, cards, (_, row) => Stages[row.Slug], hasEnded ?? (_ => false));

    private static SlugEntry Entry(ProjectLayout layout, string slug) =>
        layout.Objectives.SelectMany(objective => objective.Entries).Single(entry => entry.Row.Slug == slug);

    [Test]
    public void ACard_SitsAtItsSlug()
    {
        var layout = Lay([Card("work", "p", "second-slug")]);

        Assert.That(Entry(layout, "second-slug").CardKeys, Is.EqualTo(new[] { "work" }));
    }

    [Test]
    public void AnEarlierSlugOfTheSameWorkstream_ShowsAtItsOwnStage_NotAsACard()
    {
        var first = Entry(Lay([Card("work", "p", "second-slug")]), "first-slug");

        Assert.That(first.CardKeys, Is.Empty);
        Assert.That(first.IsEarlierSlug, Is.True);
        Assert.That(first.Stage.Position, Is.EqualTo(PipelineStage.Spec));
        Assert.That(first.IsNotStarted, Is.False);
    }

    [Test]
    public void ASlugWithNoCardAndNothingWritten_IsNotStarted()
    {
        var waiting = Entry(Lay([]), "waiting-slug");

        Assert.That((waiting.IsNotStarted, waiting.IsEarlierSlug), Is.EqualTo((true, false)));
    }

    [Test]
    public void ACardWithNoSlug_IsUnderNoSlugYet_AndOneWithAnUnlistedSlug_UnderNotInTheProjectFile()
    {
        var layout = Lay([Card("fresh", "p"), Card("stray", "p", "gone-slug")]);

        Assert.That(layout.NoSlugYet, Is.EqualTo(new[] { "fresh" }));
        Assert.That(layout.NotInProjectFile, Is.EqualTo(new[] { "stray" }));
    }

    [Test]
    public void CardsOfOtherProjects_AdHocCards_AndArchivedCards_AreLeftOut()
    {
        var layout = Lay([Card("elsewhere", "q", "first-slug"), Card("ad-hoc"), Card("old", "p", "first-slug", archived: true)]);

        Assert.That(layout.Objectives.SelectMany(objective => objective.Entries).SelectMany(entry => entry.CardKeys), Is.Empty);
        Assert.That(layout.NoSlugYet.Concat(layout.NotInProjectFile), Is.Empty);
    }

    [Test]
    public void AVerifier_SitsInVerifyBesideItsSubject_UntilItEnds()
    {
        var verifier = Card("checker", "p", "other-slug", WorkstreamRole.Verifier);
        Workstream[] cards = [Card("work", "p", "other-slug"), verifier];

        Assert.That(Entry(Lay(cards), "other-slug").VerifierKeys, Is.EqualTo(new[] { "checker" }));
        Assert.That(Entry(Lay(cards), "other-slug").CardKeys, Is.EqualTo(new[] { "work" }), "a verifier is not its subject's card");
        Assert.That(SlugEntry.VerifierStage, Is.EqualTo(PipelineStage.Verify));
        Assert.That(Entry(Lay(cards, card => card.Key == "checker"), "other-slug").VerifierKeys, Is.Empty, "it leaves the row when it ends");
    }

    [Test]
    public void ThePrimarysPullRequestGate_IsNotReadYet()
    {
        Assert.That(Lay([]).MainPullRequest, Is.EqualTo(GateState.NotReadYet));
    }
}
