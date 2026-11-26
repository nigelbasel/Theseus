using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A project tab carries an amber dot while its project's Waits for you has anything (board-small-changes item 2, Nigel,
/// 2026-11-26), and the count is in its tooltip and its name, so the state is never colour alone.
/// </summary>
[TestFixture]
public class ProjectTabWaitsTests
{
    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
        WriteTheseus(_folder);
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private ProjectTabViewModel Tab(int waits, string open = "elsewhere") =>
        ProjectTabViewModel.For(ProjectCatalog.Read(_folder.Folder), open, _ => null, waitsOf: _ => waits).Single();

    [Test]
    public void ATabWhoseProjectHasWaits_HasTheDot_AndSaysHowManyInItsNameAndTooltip()
    {
        var tab = Tab(3);

        Assert.That((tab.HasWaiting, tab.WaitingText), Is.EqualTo((true, "3 waiting for you")));
        Assert.That(tab.AutomationName, Is.EqualTo("Theseus bootstrap, 3 waiting for you"));
        Assert.That(tab.ToolTip, Is.EqualTo($"Open Theseus bootstrap (Ctrl+1){Environment.NewLine}3 waiting for you"));
    }

    [Test]
    public void OneWait_IsSaidInTheSingular() =>
        Assert.That(Tab(1).WaitingText, Is.EqualTo("1 waiting for you"));

    [Test]
    public void TheOpenTab_HasItsDotToo()
    {
        var tab = Tab(2, open: TheseusProject);

        Assert.That((tab.IsOpen, tab.HasWaiting, tab.ToolTip),
            Is.EqualTo((true, true, $"Theseus bootstrap is open{Environment.NewLine}2 waiting for you")));
    }

    [Test]
    public void ATabWithNothingWaiting_HasNoDot_AndItsNameAndTooltipAreAsBefore()
    {
        var tab = Tab(0);

        Assert.That((tab.HasWaiting, tab.AutomationName, tab.ToolTip), Is.EqualTo((false, "Theseus bootstrap", "Open Theseus bootstrap (Ctrl+1)")));
    }

    [Test]
    public void WithNoCountGiven_NoTabHasADot() =>
        Assert.That(ProjectTabViewModel.For(ProjectCatalog.Read(_folder.Folder), null, _ => null).Single().HasWaiting, Is.False);

    [Test]
    public void AChangeInATabsWaits_RedrawsTheView_SoTheDotFollowsTheWaits()
    {
        var reading = ProjectCatalog.Read(_folder.Folder).Single();

        ProjectViewModel With(int waits) => ProjectViewModel.Build(reading, TheseusPieces(_folder), LinksOn([], []),
            ProjectTabViewModel.For([reading], TheseusProject, _ => null, waitsOf: _ => waits), _views.Settings, _views.Actions);

        Assert.That(With(5).Signature, Is.Not.EqualTo(With(0).Signature));
    }
}
