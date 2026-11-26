using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// View › Project view and the toolbar's Projects open the project view with no project picked first (board-small-changes
/// item 3, Nigel, 2026-11-26): on the project last opened, or the first, and its tabs switch from there.
/// </summary>
[TestFixture]
public class ProjectViewTargetTests
{
    [Test]
    public void WithNoProjectOpenedYet_ItIsTheFirst() =>
        Assert.That(ProjectViewTarget.Choose(["theseus", "build-mcp"], null), Is.EqualTo("theseus"));

    [Test]
    public void AfterAProjectWasOpened_ItIsThatOne() =>
        Assert.That(ProjectViewTarget.Choose(["theseus", "build-mcp"], "build-mcp"), Is.EqualTo("build-mcp"));

    [Test]
    public void TheLastOpened_IsMatchedWhateverItsCase() =>
        Assert.That(ProjectViewTarget.Choose(["theseus", "build-mcp"], "Build-MCP"), Is.EqualTo("build-mcp"));

    [Test]
    public void ALastOpenedProjectThatNoLongerReads_GivesTheFirst() =>
        Assert.That(ProjectViewTarget.Choose(["theseus", "build-mcp"], "gone"), Is.EqualTo("theseus"));

    [Test]
    public void WithNoProjectThatReads_ThereIsNone() =>
        Assert.That(ProjectViewTarget.Choose([], "theseus"), Is.Null);
}
