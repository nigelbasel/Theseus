using Theseus.Core.Domain;
using Theseus.Core.Links;
using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Projects;

/// <summary>The tree from hand-over links, then worked on, and report-back marks (1.1d AC 11 to 14).</summary>
[TestFixture]
public class PieceTreeTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private PieceTree BuildYourMarketTree()
    {
        var links = BuildYourMarketLinks();

        return PieceTree.Build(BuildYourMarketPieces(_folder, links: links), LinksOn(links, BuildYourMarketBoard()));
    }

    private static PieceNode Node(PieceTree tree, string name) => tree.All.Single(node => node.Piece.Name == name);

    [Test]
    public void TheTheseusFixture_HasTwelveRootsAndNoChildren_AndElevenThenWorkedOnLinks()
    {
        var tree = PieceTree.Build(TheseusPieces(_folder), LinksOn([], [Card(Git22)]));

        Assert.That(tree.Roots, Has.Count.EqualTo(12));
        Assert.That(tree.Roots, Has.All.Matches<PieceNode>(node => node.Children.Count == 0));
        Assert.That(tree.ThenWorkedOn, Has.Count.EqualTo(11));
        Assert.That(tree.ThenWorkedOn[0].From.Name, Is.EqualTo("method-kit"));
        Assert.That(tree.ThenWorkedOn[0].To.Name, Is.EqualTo("holdout-scenarios"));
        Assert.That(tree.ThenWorkedOn[^1].To.Name, Is.EqualTo("projects-and-method-ui"));
        Assert.That(tree.ThenWorkedOn.Zip(tree.ThenWorkedOn.Skip(1)), Has.All.Matches<(ThenWorkedOnLink, ThenWorkedOnLink)>(
            pair => pair.Item1.To == pair.Item2.From));
    }

    [Test]
    public void TheBuildYourMarketFixture_HasOneRoot_WithSevenInParallel_EndingWithTheUnlistedCard()
    {
        var tree = BuildYourMarketTree();
        var root = tree.Roots.Single();

        Assert.That(root.Piece.Name, Is.EqualTo(ServerSpec));
        Assert.That(root.IsParallel, Is.True);
        Assert.That(root.ChildCount, Is.EqualTo(7));
        Assert.That(root.Children.Select(child => child.Piece.Name), Is.EqualTo(new[]
        {
            "bym-mcp-preconditions", "bym-mcp-identity", "bym-mcp-config", "bym-mcp-read-api", "bym-mcp-host", "bym-mcp-infra",
            ClientFixes,
        }));
    }

    [Test]
    public void BymMcpIdentity_HasOneChild_TheRedirectAnchoring()
    {
        var identity = Node(BuildYourMarketTree(), "bym-mcp-identity");

        Assert.That(identity.Children.Select(child => child.Piece.Name), Is.EqualTo(new[] { RedirectAnchoring }));
        Assert.That(identity.IsParallel, Is.False);
    }

    [Test]
    public void AHandOver_SitsUnderThePieceItsParentPieceNames()
    {
        var links = new[] { NewLink(LinkKind.HandsOver, Git22, "a-helper") with { ParentPiece = "project-view" } };
        var pieces = PiecesOfWork.Build(WriteTheseus(_folder), _ => null, [Card(Git22), Card("a-helper")], links);

        var tree = PieceTree.Build(pieces, LinksOn(links, [Card(Git22), Card("a-helper")]));

        Assert.That(Node(tree, "project-view").Children.Select(child => child.Piece.Name), Is.EqualTo(new[] { "a-helper" }));
        Assert.That(Node(tree, "projects-and-method-ui").Children, Is.Empty, "not the card's current piece (1.1f D16)");
    }

    [Test]
    public void AHandOverWithNoParentPiece_InAOneRoundProject_SitsUnderTheFromCardsCurrentPiece()
    {
        var links = new[] { NewLink(LinkKind.HandsOver, Git22, "a-helper") };
        var pieces = PiecesOfWork.Build(WriteTheseus(_folder), _ => null, [Card(Git22), Card("a-helper")], links);

        var tree = PieceTree.Build(pieces, LinksOn(links, [Card(Git22), Card("a-helper")]));

        Assert.That(Node(tree, "projects-and-method-ui").Children.Select(child => child.Piece.Name), Is.EqualTo(new[] { "a-helper" }),
            "as 1.1d placed it (decision G8)");
    }

    [Test]
    public void AHandOverWhoseParentPieceIsInAnotherProject_PlacesNothingHere()
    {
        var links = new[] { NewLink(LinkKind.HandsOver, Git22, "a-helper") with { ParentPiece = "elsewhere" } };
        var pieces = PiecesOfWork.Build(WriteTheseus(_folder), _ => null, [Card(Git22), Card("a-helper")], links);

        Assert.That(pieces.Select(piece => piece.Name), Has.None.EqualTo("a-helper"));
    }

    [Test]
    public void ASecondParent_IsIgnored()
    {
        var cards = BuildYourMarketBoard();
        var links = BuildYourMarketLinks()
            .Append(NewLink(LinkKind.HandsOver, "bym-mcp-w2-config-p5-refusal", RedirectAnchoring))
            .ToList();

        var tree = PieceTree.Build(BuildYourMarketPieces(_folder, cards, links), LinksOn(links, cards));

        Assert.That(Node(tree, "bym-mcp-identity").Children.Select(child => child.Piece.Name), Is.EqualTo(new[] { RedirectAnchoring }));
        Assert.That(Node(tree, "bym-mcp-config").Children, Is.Empty);
    }

    [Test]
    public void ACycle_IsBrokenAtThePieceMetFirst()
    {
        var reading = WriteBuildYourMarket(_folder);
        var cards = BuildYourMarketBoard();
        var identity = "bym-mcp-w1-identityserver-resources-and-staff-client";
        var config = "bym-mcp-w2-config-p5-refusal";
        Link[] links = [NewLink(LinkKind.HandsOver, identity, config), NewLink(LinkKind.HandsOver, config, identity)];

        var tree = PieceTree.Build(PiecesOfWork.Build(reading, _ => null, cards, links), LinksOn(links, cards));

        Assert.That(tree.Roots.Select(root => root.Piece.Name), Does.Contain("bym-mcp-identity").And.Not.Contain("bym-mcp-config"));
        Assert.That(Node(tree, "bym-mcp-identity").Children.Select(child => child.Piece.Name), Is.EqualTo(new[] { "bym-mcp-config" }));
    }

    [Test]
    public void ARemovedHandOver_BetweenListedPieces_PlacesNothing()
    {
        var cards = BuildYourMarketBoard();
        var links = BuildYourMarketLinks()
            .Select(link => link.Kind == LinkKind.HandsOver && link.To == "bym-mcp-w2-config-p5-refusal"
                ? link with { RemovedUtc = DateTimeOffset.UnixEpoch.AddDays(3) }
                : link)
            .ToList();

        var tree = PieceTree.Build(BuildYourMarketPieces(_folder, cards, links), LinksOn(links, cards));

        Assert.That(tree.Roots.Select(root => root.Piece.Name), Is.EqualTo(new[] { ServerSpec, "bym-mcp-config" }));
        Assert.That(tree.Roots[0].Children.Select(child => child.Piece.Name), Does.Not.Contain("bym-mcp-config"));
        Assert.That(tree.Roots[0].ChildCount, Is.EqualTo(6));
    }

    [Test]
    public void SixChildren_HaveReportedBack_AndTheUnlistedOneReportsBack()
    {
        var tree = BuildYourMarketTree();
        var children = tree.Roots.Single().Children;

        Assert.That(children.Take(6).Select(child => child.ReportBack?.Text),
            Has.All.EqualTo($"↩ reported back to Specify the BuildYourMarket MCP server"));
        Assert.That(children[6].Piece.Name, Is.EqualTo(ClientFixes));
        Assert.That(children[6].ReportBack?.Text, Is.EqualTo($"↩ reports back to Specify the BuildYourMarket MCP server"));
        Assert.That(tree.Roots.Single().ReportBack, Is.Null);
        Assert.That(Node(tree, RedirectAnchoring).ReportBack, Is.Null);
    }

    [TestCase(LinkState.Reported, "↩ reported back to a-parent", false)]
    [TestCase(LinkState.Satisfied, "↩ reported back to a-parent", false)]
    [TestCase(LinkState.Waiting, "↩ reports back to a-parent", false)]
    [TestCase(LinkState.NoReportYet, "↩ reports back to a-parent", false)]
    [TestCase(LinkState.Lineage, "↩ reports back to a-parent", false)]
    [TestCase(LinkState.ReportLost, "↩ report to a-parent lost", true)]
    public void AReportBackMark_SaysWhereTheReportIs(LinkState state, string text, bool lost)
    {
        var mark = new ReportBackMark("a-parent", state);

        Assert.That(mark.Text, Is.EqualTo(text));
        Assert.That(mark.IsLost, Is.EqualTo(lost));
    }

    [Test]
    public void AReportLost_IsMarkedLost_OnTheTree()
    {
        var cards = BuildYourMarketBoard();
        var links = BuildYourMarketLinks().Where(link => link.From != ClientFixes || link.Kind != LinkKind.ReportsBack)
            .Append(NewLink(LinkKind.ReportsBack, ClientFixes, ServerSpec) with { Output = "report.md" })
            .ToList();
        var linkCards = cards.Select(card => new LinkCard(card.Key, card.Title, card.Key == ClientFixes, false, false,
            card.Key == ClientFixes ? DateTimeOffset.UnixEpoch.AddDays(1) : null));

        var tree = PieceTree.Build(BuildYourMarketPieces(_folder, cards, links), new LinkStatus(links, linkCards));

        Assert.That(Node(tree, ClientFixes).ReportBack, Is.EqualTo(new ReportBackMark("Specify the BuildYourMarket MCP server", LinkState.ReportLost)));
    }

    [Test]
    public void ABusyCard_DoesNotMoveItsPieceInTheTree()
    {
        var cards = BuildYourMarketBoard().Select(card => card with { Lifecycle = SessionLifecycle.Running }).ToList();
        var links = BuildYourMarketLinks();

        Assert.That(PieceTree.Build(BuildYourMarketPieces(_folder, cards, links), LinksOn(links, cards)).Roots.Single().ChildCount,
            Is.EqualTo(7));
    }
}
