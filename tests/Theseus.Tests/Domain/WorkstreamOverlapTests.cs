using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class WorkstreamOverlapTests
{
    [Test]
    public void Find_CatchesTwoWorkstreamsPointedAtOneHandoverDocument()
    {
        var clashes = WorkstreamOverlap.Find(
        [
            ("contact-forms", @"C:\dev\HANDOFF.md", null),
            ("job-queue", @"C:\dev\HANDOFF.md", null),
        ]);

        Assert.That(clashes, Has.Count.EqualTo(1));
        Assert.That(clashes[0].Kind, Is.EqualTo(ClashKind.Handoff));
        Assert.That(clashes[0].Workstreams, Is.EquivalentTo(new[] { "contact-forms", "job-queue" }));
    }

    [Test]
    public void Find_MatchesAPathTheWayTheFileSystemDoes()
    {
        var clashes = WorkstreamOverlap.Find(
        [
            ("contact-forms", @"C:\dev\Handoff.md", null),
            ("job-queue", @"c:\dev\handoff.md", null),
        ]);

        Assert.That(clashes, Has.Count.EqualTo(1),
            "two paths differing only in case are one file, and one file is the whole problem");
    }

    [Test]
    public void Find_CatchesAJiraIssueOnTwoCards()
    {
        var clashes = WorkstreamOverlap.Find(
        [
            ("contact-forms", @"C:\a.md", ["NDC-742", "APPS-1"]),
            ("job-queue", @"C:\b.md", ["NDC-742"]),
        ]);

        Assert.That(clashes, Has.Count.EqualTo(1));
        Assert.That(clashes[0].Kind, Is.EqualTo(ClashKind.JiraKey));
        Assert.That(clashes[0].What, Is.EqualTo("NDC-742"));
    }

    [Test]
    public void Find_TurnsAJiraIssueSharedByLinkedCardsIntoALinkedShare()
    {
        var clashes = WorkstreamOverlap.Find(
        [
            ("auth-research", @"C:\a.md", ["APPS-1412"]),
            ("token-spike", @"C:\b.md", ["APPS-1412"]),
        ], [("auth-research", "token-spike")]);

        Assert.That(clashes.Single().Kind, Is.EqualTo(ClashKind.LinkedJiraKey));
        Assert.That(WorkstreamOverlap.Describe("token-spike", clashes), Is.Empty);
        Assert.That(WorkstreamOverlap.DescribeLinked("token-spike", clashes),
            Is.EqualTo("shares APPS-1412 with linked card auth-research"));
    }

    [Test]
    public void Find_CountsCardsLinkedThroughAThirdAsLinked()
    {
        var clashes = WorkstreamOverlap.Find(
        [
            ("auth-research", @"C:\a.md", ["APPS-1412"]),
            ("token-spike", @"C:\b.md", ["APPS-1412"]),
            ("cache-spike", @"C:\c.md", ["APPS-1412"]),
        ], [("auth-research", "token-spike"), ("cache-spike", "auth-research")]);

        Assert.That(clashes.Single().Kind, Is.EqualTo(ClashKind.LinkedJiraKey));
    }

    [Test]
    public void Find_StillClashesWhenOneCardSharingTheIssueIsNotLinked()
    {
        var clashes = WorkstreamOverlap.Find(
        [
            ("auth-research", @"C:\a.md", ["APPS-1412"]),
            ("token-spike", @"C:\b.md", ["APPS-1412"]),
            ("stranger", @"C:\c.md", ["APPS-1412"]),
        ], [("auth-research", "token-spike")]);

        Assert.That(clashes.Single().Kind, Is.EqualTo(ClashKind.JiraKey));
    }

    [Test]
    public void Find_NeverExemptsAHandoffSharedByLinkedCards()
    {
        var clashes = WorkstreamOverlap.Find(
        [
            ("auth-research", @"C:\HANDOFF.md", null),
            ("token-spike", @"C:\HANDOFF.md", null),
        ], [("auth-research", "token-spike")]);

        Assert.That(clashes.Single().Kind, Is.EqualTo(ClashKind.Handoff));
    }

    [Test]
    public void Find_SaysNothingWhenNothingIsShared()
    {
        Assert.That(WorkstreamOverlap.Find(
        [
            ("contact-forms", @"C:\a.md", ["APPS-1"]),
            ("job-queue", @"C:\b.md", ["APPS-2"]),
        ]), Is.Empty);
    }

    [Test]
    public void Find_IgnoresWorkstreamsWithNoHandoffResolvedYet()
    {
        Assert.That(WorkstreamOverlap.Find(
        [
            ("contact-forms", null, null),
            ("job-queue", "   ", null),
        ]), Is.Empty, "two unknowns are not the same file");
    }

    [Test]
    public void Find_CountsAWorkstreamOnceHoweverManyTimesItNamesTheSameIssue()
    {
        var clashes = WorkstreamOverlap.Find(
        [
            ("contact-forms", @"C:\a.md", ["NDC-742", "NDC-742"]),
            ("job-queue", @"C:\b.md", ["NDC-742"]),
        ]);

        Assert.That(clashes[0].Workstreams, Has.Count.EqualTo(2));
    }

    [Test]
    public void Find_CopesWithNothingAtAll()
    {
        Assert.That(WorkstreamOverlap.Find([]), Is.Empty);
    }

    [Test]
    public void Describe_TellsACardWhoItIsSharingWithAndWhyThatMatters()
    {
        var clashes = WorkstreamOverlap.Find(
        [
            ("contact-forms", @"C:\dev\HANDOFF.md", null),
            ("job-queue", @"C:\dev\HANDOFF.md", null),
        ]);

        var said = WorkstreamOverlap.Describe("contact-forms", clashes);

        Assert.Multiple(() =>
        {
            Assert.That(said, Does.Contain("job-queue"), "naming the other one is the useful part");
            Assert.That(said, Does.Not.Contain("contact-forms"), "a card does not need telling who it is");
            Assert.That(said, Does.Contain("overwrite"), "and it has to say what the consequence is");
        });
    }

    [Test]
    public void Describe_SaysNothingToACardThatShareNothing()
    {
        var clashes = WorkstreamOverlap.Find(
        [
            ("contact-forms", @"C:\dev\HANDOFF.md", null),
            ("job-queue", @"C:\dev\HANDOFF.md", null),
        ]);

        Assert.That(WorkstreamOverlap.Describe("admin-console", clashes), Is.Empty);
    }

    [Test]
    public void Describe_RunsSeveralClashesTogetherForOneCard()
    {
        var clashes = WorkstreamOverlap.Find(
        [
            ("contact-forms", @"C:\dev\HANDOFF.md", ["NDC-742"]),
            ("job-queue", @"C:\dev\HANDOFF.md", ["NDC-742"]),
        ]);

        var said = WorkstreamOverlap.Describe("contact-forms", clashes);

        Assert.That(said, Does.Contain("handover notes"));
        Assert.That(said, Does.Contain("NDC-742"));
    }
}
