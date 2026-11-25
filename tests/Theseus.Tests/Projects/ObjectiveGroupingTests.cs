using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.Projects;

/// <summary>
/// The pieces of work under the project file's phases and objectives (1.1d AC 46, 47, 51): every phase and objective in
/// file order, each piece under its own objective, the pieces no file lists after them, and the marks that stand for the
/// Flow's lines, on both of the mockup's projects.
/// </summary>
[TestFixture]
public class ObjectiveGroupingTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private static IEnumerable<(string Objective, string[] Pieces)> Shape(IReadOnlyList<PhaseGroup> phases) =>
        phases.SelectMany(phase => phase.Objectives)
            .Select(group => (group.Objective!.Number, group.Pieces.Select(piece => piece.Name).ToArray()));

    [Test]
    public void OnTheseus_EveryObjectiveIsPresent_InFileOrder_WithItsPieces()
    {
        var reading = WriteTheseus(_folder);
        var pieces = TheseusPieces(_folder);
        var phases = ObjectiveGrouping.Phases(reading.File!, pieces);

        Assert.That(phases.Select(phase => phase.Phase.Name), Is.EqualTo(new[] { "Seed the method", "Evidence" }));
        Assert.That(Shape(phases), Is.EqualTo(new (string, string[])[]
        {
            ("0.1", ["method-kit"]),
            ("0.2", ["holdout-scenarios"]),
            ("0.3", ["readme-runtime", "guard-mentions", "stop-timing", "card-branches", "parked-cores", "resumed-stop"]),
            ("1.0", ["verifier-card"]),
            ("1.1", ["project-view", "project-home-folders", "projects-and-method-ui"]),
        }));
        Assert.That(ObjectiveGrouping.Unlisted(pieces), Is.Null, "every Theseus piece is listed");
    }

    [Test]
    public void OnBuildYourMarket_EveryObjectiveAndTheEmptyPhaseAreKept_AndTheUnlistedPiecesComeLast()
    {
        var pieces = BuildYourMarketPieces(_folder);
        var phases = ObjectiveGrouping.Phases(Read(_folder, BuildYourMarket).File!, pieces);

        Assert.That(phases.Select(phase => (phase.Phase.Number, phase.Objectives.Count)), Is.EqualTo(new[] { (0, 1), (1, 6), (2, 0) }));
        Assert.That(Shape(phases), Is.EqualTo(new (string, string[])[]
        {
            ("0.1", [ServerSpec]),
            ("1.1", ["bym-mcp-preconditions"]),
            ("1.2", ["bym-mcp-identity", "bym-mcp-config"]),
            ("1.3", ["bym-mcp-read-api"]),
            ("1.4", ["bym-mcp-host", "bym-mcp-infra"]),
            ("1.5", []),
            ("1.6", []),
        }));
        Assert.That(ObjectiveGrouping.Unlisted(pieces)!.Pieces.Select(piece => piece.Name), Is.EqualTo(new[] { RedirectAnchoring, ClientFixes }), "in link order, after the listed pieces");
        Assert.That(phases.Select(phase => phase.IsDone), Is.EqualTo(new[] { true, false, false }), "an empty phase is not done");
    }

    [Test]
    public void OnBuildYourMarket_TheSpecHandingOnInto1Point1To1Point4_IsMarkedAtBothEnds()
    {
        var pieces = BuildYourMarketPieces(_folder);
        var cards = BuildYourMarketBoard();
        var marks = ObjectiveGrouping.Marks(PieceTree.Build(pieces, LinksOn(BuildYourMarketLinks(), cards)));
        string[] MarksOf(string piece) => [.. marks.Single(pair => pair.Key.Name == piece).Value];

        Assert.That(MarksOf(ServerSpec), Is.EqualTo(new[]
        {
            "→ handed on to bym-mcp-preconditions, bym-mcp-identity, bym-mcp-config, bym-mcp-read-api, bym-mcp-host, bym-mcp-infra, "
            + ClientFixes,
        }));

        foreach (var child in new[] { "bym-mcp-preconditions", "bym-mcp-config", "bym-mcp-read-api", "bym-mcp-host", "bym-mcp-infra", ClientFixes })
        {
            Assert.That(MarksOf(child), Is.EqualTo(new[] { $"← handed on from Specify the BuildYourMarket MCP server" }), child);
        }

        Assert.That(MarksOf("bym-mcp-identity"), Is.EqualTo(new[] { $"← handed on from Specify the BuildYourMarket MCP server", $"→ handed on to {RedirectAnchoring}" }));
        Assert.That(MarksOf(RedirectAnchoring), Is.EqualTo(new[] { "← handed on from Identity and Config" }));
    }

    [Test]
    public void OnTheseus_ThenWorkedOn_IsMarkedOnlyWhereItCrossesIntoAnotherObjective()
    {
        var pieces = TheseusPieces(_folder);
        var marks = ObjectiveGrouping.Marks(PieceTree.Build(pieces, LinksOn([], [Card(Git22)])));
        string[] MarksOf(string piece) => [.. marks.Single(pair => pair.Key.Name == piece).Value];

        Assert.That(MarksOf("method-kit"), Is.EqualTo(new[] { "┈ then worked on holdout-scenarios" }));
        Assert.That(MarksOf("holdout-scenarios"), Is.EqualTo(new[] { "┈ worked on after method-kit", "┈ then worked on readme-runtime" }));
        Assert.That(MarksOf("readme-runtime"), Is.EqualTo(new[] { "┈ worked on after holdout-scenarios" }));
        Assert.That(MarksOf("stop-timing"), Is.Empty, "within objective 0.3 the file's order shows it");
        Assert.That(MarksOf("resumed-stop"), Is.EqualTo(new[] { "┈ then worked on verifier-card" }));
        Assert.That(MarksOf("verifier-card"), Is.EqualTo(new[] { "┈ worked on after resumed-stop", "┈ then worked on project-view" }));
        Assert.That(MarksOf("project-view"), Is.EqualTo(new[] { "┈ worked on after verifier-card" }));
        Assert.That(MarksOf("projects-and-method-ui"), Is.Empty);
    }

    [Test]
    public void APhase_IsDone_OnlyWhenItHasObjectives_AndEveryOneIsDone()
    {
        var reading = WriteOne(_folder, "feature", objectiveDone: true);
        var phase = ObjectiveGrouping.Phases(reading.File!, []).Single();

        Assert.That((phase.IsDone, phase.ObjectivesDone), Is.EqualTo((true, 1)));
        Assert.That(phase.Objectives.Single().Pieces, Is.Empty, "an objective with no pieces is still there");
    }
}
