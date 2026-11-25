using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Fold memory (1.1d AC 21, 33): stored as the owner leaves it under pieces:Project:&lt;project&gt;, folded by default,
/// and nothing opens or folds by itself.
/// </summary>
[TestFixture]
public class ProjectViewFoldTests
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

    private IReadOnlyList<string> Stored(string project) =>
        _views.Settings.Settings.ViewFolds.GetValueOrDefault(PieceFolds.KeyFor(project)) ?? [];

    private static PieceNodeViewModel Box(ProjectViewModel view, string piece) => view.AllNodes.Single(node => node.Name == piece);

    [Test]
    public void EveryBox_IsFolded_TheFirstTime()
    {
        Assert.That(_views.Theseus().AllNodes, Has.None.Matches<PieceNodeViewModel>(node => node.IsOpen));
    }

    [Test]
    public void ABoxToggled_IsWrittenAtOnce_UnderItsProjectsKey()
    {
        var view = _views.Theseus();

        view.Toggle(Box(view, "stop-timing"));
        Assert.That(Stored(TheseusProject), Is.EqualTo(new[] { "stop-timing=open" }));

        view.Toggle(Box(view, "stop-timing"));
        Assert.That(Stored(TheseusProject), Is.EqualTo(new[] { "stop-timing=folded" }));
    }

    [Test]
    public void CollapseAllAndExpandAll_WriteStar_AndDropTheBoxesOwnEntries()
    {
        var view = _views.Theseus();
        view.Toggle(Box(view, "stop-timing"));

        view.ExpandAllCommand.Execute(null);
        Assert.That(Stored(TheseusProject), Is.EqualTo(new[] { "*=open" }));

        view.CollapseAllCommand.Execute(null);
        Assert.That(Stored(TheseusProject), Is.EqualTo(new[] { "*=folded" }));
    }

    [Test]
    public void OpenWhatNeedsAttention_WritesEveryBox()
    {
        var view = _views.Theseus();

        view.OpenWhatNeedsAttentionCommand.Execute(null);

        Assert.That(Stored(TheseusProject), Has.Count.EqualTo(12 + 1), "every box, and its round's to do heading, folded");
        Assert.That(Stored(TheseusProject), Does.Contain("project-view=open").And.Contain("stop-timing=folded"));
    }

    [Test]
    public void ARestart_OpensEachBoxAsItWasLeft()
    {
        var view = _views.Theseus();
        view.ExpandAllCommand.Execute(null);
        view.Toggle(Box(view, "stop-timing"));

        var restarted = _views.Theseus();

        Assert.That(restarted.AllNodes.Where(node => !node.IsOpen).Select(node => node.Name), Is.EqualTo(new[] { "stop-timing" }));
    }

    [Test]
    public void ARowAddedAfterCollapseAll_IsFolded_AndAfterExpandAll_IsOpen()
    {
        var view = _views.One("feature", Files("research.md"));
        view.CollapseAllCommand.Execute(null);

        var path = Path.Combine(_folder.Home(OneProject), "project.md");
        AddRow(path, "a-second-piece");
        var refreshed = _views.Of(Read(_folder, OneProject), [Card(ACard)]);

        Assert.That(Box(refreshed, "a-second-piece").IsOpen, Is.False);

        refreshed.ExpandAllCommand.Execute(null);
        AddRow(path, "a-third-piece");

        Assert.That(Box(_views.Of(Read(_folder, OneProject), [Card(ACard)]), "a-third-piece").IsOpen, Is.True);
    }

    private static void AddRow(string path, string piece)
    {
        const string first = $"| {ACard} | {APiece} | owner/{APiece} | a-repo |\n";

        File.WriteAllText(path, File.ReadAllText(path).Replace(first, first + $"| {ACard} | {piece} | owner/{piece} | a-repo |\n",
            StringComparison.Ordinal));
    }

    [Test]
    public void ABoxThatBecomesWaitingOnYou_StaysFolded_AndShowsItInItsStatusAndStrip()
    {
        var idle = _views.One("feature", Files("research.md"));
        var asking = _views.One("feature", Files("research.md"), SessionLifecycle.NeedsAttention, idle);
        var box = asking.AllNodes.Single();

        Assert.That(box.IsOpen, Is.False);
        Assert.That(box.State, Is.EqualTo(StageNodeState.WaitingOnYou));
        Assert.That(box.StatusText, Does.StartWith("◆ Waiting for you"));
        Assert.That(box.Stages.Select(stage => stage.State), Does.Contain(StageNodeState.WaitingOnYou));
    }

    [Test]
    public void EachProject_KeepsItsOwnFolds()
    {
        _views.Theseus().ExpandAllCommand.Execute(null);

        Assert.That(_views.BuildYourMarket().AllNodes, Has.None.Matches<PieceNodeViewModel>(node => node.IsOpen));
    }
}
