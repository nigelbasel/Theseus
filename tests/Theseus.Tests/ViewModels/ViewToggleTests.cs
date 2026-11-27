using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The one Board / Projects toggle (project-view-as-tabs AC 5; Nigel 2026-11-26: "we should have a single toolbar button
/// that changes depending on context"): its words and glyph follow where you are, and from the board it opens the project
/// view where it was last left, All projects on first use when that tab exists (agreed with 1.5b).
/// </summary>
[TestFixture]
public class ViewToggleTests
{
    [Test]
    public void OnTheBoard_ItSaysProjects_AndInTheViewItSaysBoard()
    {
        Assert.Multiple(() =>
        {
            Assert.That((ViewToggle.Text(inView: false), ViewToggle.Glyph(inView: false)), Is.EqualTo(("Projects", "")));
            Assert.That((ViewToggle.Text(inView: true), ViewToggle.Glyph(inView: true)), Is.EqualTo(("Board", "")));
            Assert.That(ViewToggle.ToolTip(inView: true), Does.Contain("Ctrl+B"));
            Assert.That(ViewToggle.ToolTip(inView: false), Does.Contain("Ctrl+B"));
        });
    }

    [Test]
    public void Target_OnFirstUse_IsAllProjects_WhenThatTabExists()
    {
        var target = ViewToggle.Target(["theseus", "bug-fixing"], lastProject: null, lastWasAllProjects: false, hasAllProjects: true);

        Assert.That(target, Is.EqualTo(new ViewTarget(AllProjects: true, Project: null)));
    }

    [Test]
    public void Target_IsTheProjectLastOpen()
    {
        var target = ViewToggle.Target(["theseus", "bug-fixing"], "bug-fixing", lastWasAllProjects: false, hasAllProjects: true);

        Assert.That(target, Is.EqualTo(new ViewTarget(false, "bug-fixing")));
    }

    [Test]
    public void Target_IsAllProjects_WhenThatWasWhereTheViewWasLeft()
    {
        var target = ViewToggle.Target(["theseus"], "theseus", lastWasAllProjects: true, hasAllProjects: true);

        Assert.That(target, Is.EqualTo(new ViewTarget(true, null)));
    }

    [Test]
    public void Target_WithoutAllProjects_IsTheLastProjectOrTheFirst()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ViewToggle.Target(["theseus", "bug-fixing"], null, false, hasAllProjects: false),
                Is.EqualTo(new ViewTarget(false, "theseus")));
            Assert.That(ViewToggle.Target([], null, false, hasAllProjects: false), Is.Null);
        });
    }

    [Test]
    public void ProjectCommandsSayWhyTheyAreOff_OnTheBoardAndOnAllProjects()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ViewToggle.WhyNoProject(inView: false, allProjects: false), Is.EqualTo("Open a project to act on it"));
            Assert.That(ViewToggle.WhyNoProject(inView: true, allProjects: true), Is.EqualTo("Open a project's tab to act on it"));
            Assert.That(ViewToggle.WhyNoProject(inView: true, allProjects: false), Is.Null);
        });
    }
}
