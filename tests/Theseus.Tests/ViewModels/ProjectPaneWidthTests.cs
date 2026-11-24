using Theseus.App.ViewModels;
using Theseus.Tests.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The Properties pane's width (1.1d AC 35): 320 px at first, between 260 px and half the view with 400 px left for the
/// flow, saved when the splitter is let go and restored on opening any project; a double-click resets it.
/// </summary>
[TestFixture]
public class ProjectPaneWidthTests
{
    [TestCase(320, 1400, 320, Description = "the default fits")]
    [TestCase(100, 1400, 260, Description = "never under 260")]
    [TestCase(900, 1400, 700, Description = "never over half the view")]
    [TestCase(500, 800, 392, Description = "the flow keeps 400 px and the splitter its 8")]
    [TestCase(300, 500, 260, Description = "a view too narrow for both still keeps 260")]
    [TestCase(double.NaN, 1400, 320, Description = "a width that is not a number is the default")]
    public void AWidth_IsKeptWithinItsBounds(double width, double view, double expected)
    {
        Assert.That(ProjectPaneWidth.Clamp(width, view), Is.EqualTo(expected));
    }

    [Test]
    public void ThePane_StartsAt320_AndIsRememberedAcrossARestart_ForAnyProject()
    {
        using var folder = TestProjectsFolder.Create();
        var views = new ProjectViews(folder);

        Assert.That(views.Theseus().PaneWidth, Is.EqualTo(320));

        views.Theseus().RememberPaneWidth(420);

        Assert.That(views.Theseus().PaneWidth, Is.EqualTo(420));
        Assert.That(views.BuildYourMarket().PaneWidth, Is.EqualTo(420));
        Assert.That(views.Settings.Settings.PaneSizes["ProjectView:Pane"], Is.EqualTo(420));
    }

    [Test]
    public void ADoubleClick_ResetsItTo320_AndRemembersThat()
    {
        using var folder = TestProjectsFolder.Create();
        var views = new ProjectViews(folder);
        var view = views.Theseus();
        view.RememberPaneWidth(420);

        Assert.That(view.ResetPaneWidth(), Is.EqualTo(320));
        Assert.That(views.Theseus().PaneWidth, Is.EqualTo(320));
    }
}
