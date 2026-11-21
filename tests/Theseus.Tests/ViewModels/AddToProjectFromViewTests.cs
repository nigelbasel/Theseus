using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Add to project… from a project's view (1.1e AC 6): the boxes under "Not in project.md" offer it, on the box and in
/// Properties, and it opens the board's own window with this project chosen; listed boxes do not offer it.
/// </summary>
[TestFixture]
public class AddToProjectFromViewTests
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

    [Test]
    public void TheUnlistedBoxes_OfferAddToProject_WithThisProject_AndTheListedOnesDoNot()
    {
        var view = _views.BuildYourMarket();
        var unlisted = view.Unlisted.Single().Pieces;

        foreach (var box in unlisted)
        {
            var add = box.Actions.Single(action => action.Label == ProjectViewModel.AddToProjectLabel);
            add.Command.Execute(null);
        }

        Assert.That(_views.Ran, Is.EqualTo(unlisted.Select(box => $"AddToProject:{ProjectViewFixtures.BuildYourMarket}:{box.Piece.CardKey}")));
        Assert.That(view.AllNodes.Where(box => box.Piece.IsListed).SelectMany(box => box.Actions).Select(action => action.Label),
            Has.None.EqualTo(ProjectViewModel.AddToProjectLabel));
    }

    [Test]
    public void AnUnlistedBoxesProperties_OfferAddToProjectToo()
    {
        var view = _views.BuildYourMarket();
        var box = view.Unlisted.Single().Pieces.First();

        view.Select(box);

        Assert.That(view.Properties.Buttons.Select(button => button.Label), Does.Contain(ProjectViewModel.AddToProjectLabel));

        view.Select(view.AllNodes.First(node => node.Piece.IsListed));

        Assert.That(view.Properties.Buttons.Select(button => button.Label), Has.None.EqualTo(ProjectViewModel.AddToProjectLabel));
    }

    [Test]
    public void TheShellsCommands_PassTheCardAndTheProject_ToAddToProject()
    {
        var card = new WorkstreamCardViewModel(ProjectCards.Card(Git22));
        var asked = new List<string>();
        var notices = new List<string>();
        var actions = ProjectViewCommands.For(key => key == Git22 ? card : null,
            new RelayCommand(_ => { }), new RelayCommand(_ => { }), new RelayCommand(_ => { }),
            _ => { }, _ => { }, _ => { }, notices.Add, (chosen, project) => asked.Add($"{chosen.Key}:{project}"));

        actions.AddToProject!(TheseusProject, Git22);
        actions.AddToProject!(TheseusProject, "gone");

        Assert.That(asked, Is.EqualTo(new[] { $"{Git22}:{TheseusProject}" }));
        Assert.That(notices, Is.EqualTo(new[] { "gone is not on the board." }));
    }

    [Test]
    public void TheWindow_ChoosesTheProjectItWasAskedFrom_ElseTheOnlyOne()
    {
        IReadOnlyList<ProjectOption> two = [new("a", "A", []), new("b", "B", [])];

        Assert.That(ProjectOption.IndexToChoose(two, "B"), Is.EqualTo(1));
        Assert.That(ProjectOption.IndexToChoose(two, null), Is.EqualTo(-1));
        Assert.That(ProjectOption.IndexToChoose(two, "gone"), Is.EqualTo(-1));
        Assert.That(ProjectOption.IndexToChoose([two[0]], null), Is.EqualTo(0));
    }
}
