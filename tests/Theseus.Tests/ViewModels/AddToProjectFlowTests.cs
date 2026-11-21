using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Add to project… as the shell runs it (1.1e AC 6, review of PR 1, finding 2): asked from a project's view, the window
/// gets that project chosen; the row it makes, once written, moves the box from "Not in project.md" to its objective.
/// </summary>
[TestFixture]
public class AddToProjectFlowTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void AskedFromAView_TheWindowGetsThatProjectChosen_AndTheWrittenRowMovesTheBoxUnderItsObjective()
    {
        var reading = WriteBuildYourMarket(_folder);
        string? chosen = null;

        var addition = AddToProjectFlow.Ask([reading], RedirectAnchoring, "Redirect anchoring", "Theseus", BuildYourMarket,
            (options, name, project) =>
            {
                chosen = project;

                return new AddToProjectChoice(options.Single().Name, "1.1", name);
            }, out var notice);

        Assert.That((chosen, notice), Is.EqualTo(((string?)BuildYourMarket, (string?)null)));
        Assert.That(addition!.Row.Slug, Is.EqualTo(RedirectAnchoring));

        var file = Path.Combine(reading.Folder!, ProjectCatalog.ProjectFileName);
        File.WriteAllText(file, ProjectFileEditor.AddRow(File.ReadAllText(file), addition.Objective, addition.Row));
        var view = new ProjectViews(_folder).Of(Read(_folder, BuildYourMarket), BuildYourMarketBoard(), BuildYourMarketLinks());

        Assert.That(view.Unlisted.SelectMany(heading => heading.Pieces).Select(box => box.Name), Has.None.EqualTo(RedirectAnchoring));
        Assert.That(view.AllObjectives.Single(heading => heading.Number == "1.1").Pieces.Select(box => box.Piece.CardKey),
            Does.Contain(RedirectAnchoring));
    }

    [Test]
    public void FromTheBoard_NoProjectIsChosen_AndWithNoProjectThatReads_ItSaysSo()
    {
        var reading = WriteBuildYourMarket(_folder);
        var chosen = "unset";

        AddToProjectFlow.Ask([reading], "a-card", "A card", "repo", null, (_, _, project) =>
        {
            chosen = project ?? "none";

            return null;
        }, out _);

        Assert.That(chosen, Is.EqualTo("none"));
        Assert.That(AddToProjectFlow.Ask([], "a-card", "A card", "repo", null, (_, _, _) => null, out var notice), Is.Null);
        Assert.That(notice, Is.EqualTo(AddToProjectFlow.NoProjectReads));
    }
}
