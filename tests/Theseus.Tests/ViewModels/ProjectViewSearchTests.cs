using Theseus.App.ViewModels;
using Theseus.Tests.Projects;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A board search carried into a project view (card-search-and-tags item 7): the matching pieces are highlighted with the
/// view's own highlight, which hovering an objective does not wipe, and clearing the search clears it.
/// </summary>
[TestFixture]
public class ProjectViewSearchTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void TheMatchingPieces_AreHighlighted_AndOnlyThey()
    {
        var view = new ProjectViews(_folder).Theseus();
        var target = view.AllNodes.First();

        view.ShowSearch(node => node == target);

        Assert.Multiple(() =>
        {
            Assert.That(target.IsHighlighted, Is.True);
            Assert.That(view.AllNodes.Where(node => node != target).Any(node => node.IsHighlighted), Is.False);
            Assert.That(view.SearchHits, Is.EqualTo(new[] { target }));
        });
    }

    [Test]
    public void HoveringAnObjective_KeepsTheSearchsHighlight()
    {
        var view = new ProjectViews(_folder).Theseus();
        var target = view.AllNodes.First();
        view.ShowSearch(node => node == target);

        view.HighlightObjective("no-such-objective");
        view.HighlightObjective(null);

        Assert.That(target.IsHighlighted, Is.True);
    }

    [Test]
    public void OpeningWithASearch_OpensWhatHidesTheMatches()
    {
        var view = new ProjectViews(_folder).Theseus();
        view.CollapseAllCommand.Execute(null);
        var target = view.AllNodes.Last();
        view.ShowSearch(node => node == target);

        view.RevealSearch();

        Assert.Multiple(() =>
        {
            Assert.That(view.Rounds.Where(round => round.Boxes.Contains(target)).All(round => round.IsOpen), Is.True);
            Assert.That(view.AllObjectives.Where(heading => heading.Pieces.Contains(target)).All(heading => heading.IsOpen), Is.True);
        });
    }

    [Test]
    public void ClearingTheSearch_ClearsTheHighlight()
    {
        var view = new ProjectViews(_folder).Theseus();
        view.ShowSearch(_ => true);

        view.ShowSearch(null);

        Assert.That(view.AllNodes.Any(node => node.IsHighlighted), Is.False);
    }
}
