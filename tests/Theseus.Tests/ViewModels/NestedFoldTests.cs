using Theseus.App.ViewModels;
using Theseus.Tests.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// In the rounds and work lens, a box with boxes indented beneath it folds and unfolds them, with a chevron and a
/// remembered state, and Collapse all and Expand all include it (project-view-usability items 1 and 2, Nigel 2026-11-23).
/// </summary>
[TestFixture]
public class NestedFoldTests
{
    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private static PieceNodeViewModel Parent(ProjectViewModel view) => view.AllNodes.First(node => node.HasChildren);

    [Test]
    public void ABoxWithBoxesBeneathIt_HasABranchFold_OpenAtFirst()
    {
        var view = _views.BuildYourMarket();

        Assert.Multiple(() =>
        {
            Assert.That(Parent(view).Branch, Is.Not.Null);
            Assert.That(Parent(view).Branch!.IsOpen, Is.True, "nothing is hidden until he folds it");
            Assert.That(view.AllNodes.Where(node => !node.HasChildren).Select(node => node.Branch), Has.All.Null);
        });
    }

    [Test]
    public void FoldingABranch_IsRemembered_AndNamesWhatIsBeneath()
    {
        var view = _views.BuildYourMarket();
        var parent = Parent(view);

        parent.Branch!.ToggleCommand.Execute(null);
        var again = _views.BuildYourMarket(previous: view);

        Assert.Multiple(() =>
        {
            Assert.That(parent.Branch.IsFolded, Is.True);
            Assert.That(parent.Branch.Summary, Does.Contain(parent.Children[0].Title));
            Assert.That(Parent(again).Branch!.IsOpen, Is.False, "the fold is remembered across a rebuild");
        });
    }

    [Test]
    public void CollapseAllAndExpandAll_FoldAndOpenTheBranchesToo()
    {
        var view = _views.BuildYourMarket();

        view.CollapseAllCommand.Execute(null);
        var collapsed = view.AllNodes.Where(node => node.Branch is not null).Select(node => node.Branch!.IsOpen).ToList();
        view.ExpandAllCommand.Execute(null);
        var expanded = view.AllNodes.Where(node => node.Branch is not null).Select(node => node.Branch!.IsOpen).ToList();

        view.CollapseAllCommand.Execute(null);
        var rebuilt = _views.BuildYourMarket(previous: view);

        Assert.Multiple(() =>
        {
            Assert.That(collapsed, Is.Not.Empty.And.All.False);
            Assert.That(expanded, Is.Not.Empty.And.All.True);
            Assert.That(rebuilt.AllNodes.Select(node => node.Branch).OfType<ChildrenFold>().Select(fold => fold.IsOpen),
                Is.Not.Empty.And.All.False, "Collapse all is remembered across a rebuild");
        });
    }

    [Test]
    public void ASearchHitBeneathAFoldedBranch_OpensIt()
    {
        var view = _views.BuildYourMarket();
        var parent = Parent(view);
        var child = parent.Children[0];
        parent.Branch!.ToggleCommand.Execute(null);

        view.ShowSearch(node => node == child);
        view.RevealSearch();

        Assert.That(parent.Branch.IsOpen, Is.True);
    }

    [Test]
    public void SelectingABoxBeneathAFoldedBranch_OpensIt()
    {
        var view = _views.BuildYourMarket();
        var parent = Parent(view);
        parent.Branch!.ToggleCommand.Execute(null);

        parent.Children[0].SelectCommand.Execute(null);

        Assert.That(parent.Branch.IsOpen, Is.True, "Properties never shows a box that is not on screen");
    }
}
