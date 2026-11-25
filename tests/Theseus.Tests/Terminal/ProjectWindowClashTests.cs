using Theseus.Core.Domain;
using Theseus.Core.Terminal;

namespace Theseus.Tests.Terminal;

/// <summary>
/// An area named like a project's window keeps a window of its own while that project reads (PR 3 review, item 4), and
/// is named as before when it does not.
/// </summary>
[TestFixture]
public class ProjectWindowClashTests
{
    private static Workstream Card(string? area, string? project = null) => new(
        "a-card", "A card", "#336699", ColorIsManual: false, @"C:\work\repo", [], "prompt", 1, "New",
        IsBlocked: false, ReopenTomorrow: false, IsArchived: false, LastSessionId: null, [], StatusDocumentPath: null,
        DateTimeOffset.UtcNow)
    {
        Area = area,
        Project = project,
    };

    private static readonly IReadOnlySet<string> Readable = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "p" };

    [Test]
    public void AnAreaNamedLikeAReadableProjectsWindow_GetsItsOwnWindow()
    {
        var area = WindowGroupName.For("theseus", Card("Project P"), readableProjects: Readable);
        var project = WindowGroupName.For("theseus", Card("Dev", "p"), readableProjects: Readable);

        Assert.That(project, Is.EqualTo("theseus-project-p"));
        Assert.That(area, Is.EqualTo("theseus-project-p-area"));
    }

    [Test]
    public void WhileThatProjectDoesNotRead_TheAreaIsNamedAsBefore()
    {
        Assert.That(WindowGroupName.For("theseus", Card("Project P")), Is.EqualTo("theseus-project-p"));
        Assert.That(WindowGroupName.For("theseus", Card("Project P"), readableProjects: new HashSet<string>()), Is.EqualTo("theseus-project-p"));
    }
}
