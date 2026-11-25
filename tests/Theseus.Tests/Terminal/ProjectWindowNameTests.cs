using Theseus.Core.Domain;
using Theseus.Core.Terminal;

namespace Theseus.Tests.Terminal;

/// <summary>
/// A project's cards share one terminal window (project-view AC 19): named after Settings' window name and the project
/// file, after a card's own override and before its area and column; and with no project readable, every card gets
/// exactly the window it always got (AC 22).
/// </summary>
[TestFixture]
public class ProjectWindowNameTests
{
    private static readonly IReadOnlySet<string> Readable = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "theseus-bootstrap" };

    private static Workstream Card(string? project = null, string? area = "Dev", string lane = "In progress") => new(
        "a-card", "A card", "#336699", ColorIsManual: false, @"C:\work\repo", [], "prompt", 1, lane,
        IsBlocked: false, ReopenTomorrow: false, IsArchived: false, LastSessionId: null, [], StatusDocumentPath: null,
        DateTimeOffset.UtcNow)
    {
        Area = area,
        Project = project,
    };

    [Test]
    public void AReadableProjectsCard_OpensInTheProjectsWindow()
    {
        Assert.That(WindowGroupName.For("theseus", Card("theseus-bootstrap"), readableProjects: Readable),
            Is.EqualTo("theseus-project-theseus-bootstrap"));
    }

    [Test]
    public void TheProjectWindow_FollowsSettingsWindowName()
    {
        Assert.That(WindowGroupName.For("work", Card("theseus-bootstrap"), readableProjects: Readable),
            Is.EqualTo("work-project-theseus-bootstrap"));
    }

    [Test]
    public void ACardsOwnWindow_StillWins()
    {
        Assert.That(WindowGroupName.For("theseus", Card("theseus-bootstrap") with { WindowGroup = "Side by side" }, readableProjects: Readable),
            Is.EqualTo("side-by-side"));
    }

    [Test]
    public void TheProjectWindow_ComesBeforeTheAreasAndTheAreasKeptName()
    {
        var kept = new Dictionary<string, string> { ["Dev"] = "old-dev" };

        Assert.That(WindowGroupName.For("theseus", Card("theseus-bootstrap"), kept, Readable), Is.EqualTo("theseus-project-theseus-bootstrap"));
    }

    [Test]
    public void AnAreaNamedLikeTheProject_KeepsAWindowOfItsOwn()
    {
        Assert.That(WindowGroupName.For("theseus", Card(area: "theseus-bootstrap")), Is.EqualTo("theseus-theseus-bootstrap"));
        Assert.That(WindowGroupName.For("theseus", Card("theseus-bootstrap"), readableProjects: Readable),
            Is.Not.EqualTo(WindowGroupName.For("theseus", Card(area: "theseus-bootstrap"))));
    }

    [Test]
    public void AnUnreadableProjectsCard_OpensWhereItWouldWithoutOne()
    {
        Assert.That(WindowGroupName.For("theseus", Card("other-project"), readableProjects: Readable),
            Is.EqualTo(WindowGroupName.For("theseus", Card())));
    }

    [Test]
    public void WithNoProjectReadable_EveryCardOpensInExactlyTheWindowItAlwaysDid(
        [Values(null, "theseus-bootstrap")] string? project,
        [Values(null, "Dev", "Research and design")] string? area,
        [Values("New", "In progress")] string lane)
    {
        var card = Card(project, area, lane);
        var kept = new Dictionary<string, string> { ["Research and design"] = "research" };
        var before = WindowGroupName.For("theseus", card with { Project = null }, kept);

        Assert.That(WindowGroupName.For("theseus", card, kept), Is.EqualTo(before), "no projects listed");
        Assert.That(WindowGroupName.For("theseus", card, kept, new HashSet<string>()), Is.EqualTo(before), "none readable");
    }
}
