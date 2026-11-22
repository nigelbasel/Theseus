using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ClarityFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The Records view (brief, Records view, 2026-11-22): a row per stage with its document's purpose and state, the chosen
/// document shown, the folder one click further, and a piece with no records saying so in words.
/// </summary>
[TestFixture]
public class RecordsViewTests
{
    private TestProjectsFolder _folder = null!;

    [SetUp]
    public void SetUp() => _folder = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _folder.Dispose();

    [Test]
    public void AFeaturesRecords_AreOneRowPerStage_WithItsPurposeAndState()
    {
        var w1 = Named(BymPieces(_folder), "bym-mcp-identity");
        var rows = PieceRecords.For(w1);

        Assert.That(rows.Select(row => row.Stage), Is.EqualTo(new[] { "Research", "Spec", "Plan", "Implement", "Verify", "Review", "Compound" }));
        Assert.That(rows[1].Purpose, Is.EqualTo("What will be built, for you to accept"));
        Assert.That(rows[0].StateText, Is.EqualTo("skipped: the project's spec covered it"));
        Assert.That(rows[1].StateText, Is.EqualTo($"accepted by you on {DateTimeOffset.UnixEpoch.ToLocalTime():d MMM}"));
        Assert.That(rows[1].FileName, Is.EqualTo("spec.md"));
        Assert.That((rows[0].HasFile, rows[1].HasFile), Is.EqualTo((false, true)));
        Assert.That(PieceRecords.NoneText(w1), Is.Null);
    }

    [Test]
    public void ABugsRecords_SayWhatReproduceRuleAndFixAreFor()
    {
        var readme = Named(TheseusPieces(_folder), "readme-runtime");

        Assert.That(PieceRecords.For(readme).Take(3).Select(row => (row.Stage, row.Purpose)), Is.EqualTo(new[]
        {
            ("Reproduce", "How the fault was reproduced"),
            ("Rule", "The rule the fix must keep, for you to accept"),
            ("Fix", "What caused the fault and what was changed"),
        }));
    }

    [Test]
    public void APieceWithNoRecordsInTheProject_SaysSoInWords()
    {
        var pieces = BymPieces(_folder);

        Assert.That(PieceRecords.NoneText(Named(pieces, "identityserver-client-api-and-cli-fixes")),
            Is.EqualTo("Records are kept outside the project, so Theseus has none to show."));
        Assert.That(PieceRecords.NoneText(Named(TheseusPieces(_folder), "method-kit")),
            Is.EqualTo("Nothing is recorded in the project for this piece of work yet."));
    }

    [Test]
    public void TheWindow_ShowsTheChosenStagesDocument_AndTheFolderIsALinkFurtherOn()
    {
        var opened = new List<string>();
        var w1 = Named(BymPieces(_folder), "bym-mcp-identity");
        var model = new RecordsViewModel(w1, "Plan", opened.Add);

        Assert.That(model.Title, Is.EqualTo("Records of IdentityServer resources and staff client"));
        Assert.That((model.Selected!.Stage, model.Text), Is.EqualTo(("Plan", "# plan.md\n")));

        model.Selected = model.Entries.Single(entry => entry.Stage == "Review");
        model.OpenFolderCommand.Execute(null);

        Assert.That(model.Text, Is.EqualTo("# review.md\n"));
        Assert.That(opened, Is.EqualTo(new[] { w1.RecordsFolder }));
    }

    [Test]
    public void OpenRecords_OpensTheRecordsWindow_WhenTheAppGivesOne()
    {
        var views = new ProjectViews(_folder);
        var shown = new List<string>();
        var actions = views.Actions with { ShowRecords = (piece, stage) => shown.Add($"{piece.Name}:{stage}") };
        var view = views.Theseus(actions: actions);

        view.AllNodes.Single(node => node.Name == "project-view").Actions.Single(action => action.Label == "Open records").Command.Execute(null);

        Assert.That(shown, Is.EqualTo(new[] { "project-view:" }));
        Assert.That(views.Ran, Has.None.StartsWith("Open:"), "the folder is not opened first");
    }
}
