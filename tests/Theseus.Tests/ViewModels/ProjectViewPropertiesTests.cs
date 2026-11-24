using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The Properties pane (1.1d AC 34): a hint for nothing selected; a piece's or a stage's state in words, its rows, its
/// links with their state, its buttons, and its objective's criteria with Rename objective… (Question 3).
/// </summary>
[TestFixture]
public class ProjectViewPropertiesTests
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

    private static PieceNodeViewModel Box(ProjectViewModel view, string piece) => view.AllNodes.Single(node => node.Name == piece);

    private static PropertiesViewModel Selected(ProjectViewModel view, object item)
    {
        view.Select(item);

        return view.Properties;
    }

    private string Records(string project, string piece) => Path.Combine(_folder.Home(project), "work", piece);

    [Test]
    public void WithNothingSelected_ItSaysWhatToClick()
    {
        var pane = _views.Theseus().Properties;

        Assert.That((pane.Heading, pane.IsEmpty, pane.HasStatus), Is.EqualTo(("Properties", true, false)));
        Assert.That(pane.Hint, Is.EqualTo("Click a piece of work, or one of its stages, to see its details here: its state in words, "
                                          + "its record, its workstream and what you can do."));
    }

    [Test]
    public void APiece_ShowsItsStatusAndRows()
    {
        var view = _views.Theseus();
        var pane = Selected(view, Box(view, "project-view"));

        Assert.That((pane.Heading, pane.StatusText, pane.State), Is.EqualTo(
            ("Checks shown on cards", "Waiting for you: accept the spec.", Theseus.Core.Projects.StageNodeState.WaitingOnYou)));
        Assert.That(pane.Rows.Select(row => (row.Label, row.Value)), Is.EqualTo(new[]
        {
            ("Where", "◆ Waiting for you: accept the spec."),
            ("What it means", "Waiting for you: nothing moves until you act."),
            ("Who acts next", "You; nothing moves until you do."),
            ("Kind", "⚒ Build: Builds a change on its own branch, through spec, plan, build, check and review."),
            ("Card", "git-22 (its session is idle)"),
            ("Objective", "1.1 Checks shown on cards"),
            ("Round", "Round 1"),
            ("Records", "in the project: Open records below"),
            ("Branch", "owner/project-view"),
            ("Evidence", "open"),
            ("Accepted", "not yet"),
            ("Research", "✓ Done. Finished: its record is written and any gate you accepted."),
            ("Spec", "◆ Waiting for you: accept the spec. Waiting for you: nothing moves until you act."),
            ("Plan", "✓ Done. Finished: its record is written and any gate you accepted."),
            ("Implement", "✓ Done. Finished: its record is written and any gate you accepted."),
            ("Verify", "◐ Built, not yet checked. " + Theseus.Core.Projects.StageNodeStates.Meaning(Theseus.Core.Projects.StageNodeState.HoldoutsNotRun)),
            ("Review", "✓ Done. Finished: its record is written and any gate you accepted."),
            ("Compound", "– Not started. No work recorded for this stage."),
        }), "never a path or a folder name (1.1f H40)");
        Assert.That(pane.Buttons.Select(button => button.Label), Is.EqualTo(new[] { "Accept spec…", "Open records" }));
    }

    [Test]
    public void TheEvidenceRow_SaysNoBlock_OrNoneWritten()
    {
        var view = _views.BuildYourMarket();

        Assert.That(Selected(view, Box(view, "bym-mcp-identity")).Rows.Single(row => row.Label == "Evidence").Value, Is.EqualTo("no block"));
        Assert.That(Selected(view, Box(view, ServerSpec)).Rows.Single(row => row.Label == "Evidence").Value, Is.EqualTo("none written"));
    }

    [Test]
    public void AnUnlistedPiece_HasNoRecordsFolder_NoObjective_AndNoObjectiveSection()
    {
        var view = _views.BuildYourMarket();
        var pane = Selected(view, Box(view, ClientFixes));

        Assert.That(pane.Rows.Single(row => row.Label == "Records").Value, Is.EqualTo("none in the project"));
        Assert.That(pane.Rows.Single(row => row.Label == "Objective").Value, Is.EqualTo("not in project.md"));
        Assert.That(pane.HasObjective, Is.False);
    }

    [Test]
    public void AStage_ShowsItsWordRowsAndReason_AndOpensItsRecord()
    {
        var view = _views.Theseus();
        var spec = Box(view, "project-view").Stages.Single(stage => stage.Name == "Spec");
        var pane = Selected(view, spec);
        var record = Path.Combine(Records(TheseusProject, "project-view"), "spec.md");

        Assert.That((pane.Heading, pane.StatusText), Is.EqualTo(("Spec of Checks shown on cards", "Waiting for you")));
        Assert.That(pane.Rows.Select(row => (row.Label, row.Value)), Is.EqualTo(new[]
        {
            ("Stage", "Spec"),
            ("State", "◆ Waiting for you"),
            ("Means", "Waiting for you: nothing moves until you act."),
            ("Record", "spec.md, in its records"),
            ("Piece", "Checks shown on cards"),
            ("Worker", "git-22"),
            ("Objective", "1.1 Checks shown on cards"),
            ("Why", "accept the spec"),
        }));
        Assert.That(pane.Buttons.Select(button => button.Label), Is.EqualTo(new[] { "Accept spec…", "Open spec.md" }));

        pane.Buttons[1].Command.Execute(null);

        Assert.That(_views.Ran, Is.EqualTo(new[] { $"Open:{record}" }));
    }

    [Test]
    public void AStageNotWritten_SaysSo_AndOffersOpenRecords()
    {
        var view = _views.Theseus();
        var pane = Selected(view, Box(view, "project-view").Stages.Single(stage => stage.Name == "Compound"));

        Assert.That(pane.Rows.Single(row => row.Label == "Record").Value, Is.EqualTo("not written yet"));
        Assert.That(pane.Buttons.Select(button => button.Label), Is.EqualTo(new[] { "Open records" }));
    }

    [Test]
    public void AVerifyStage_NamesTheVerifierCheckingIt()
    {
        var links = new[] { NewLink(Theseus.Core.Links.LinkKind.ReportsBack, "checker", Git22) };
        var view = _views.Of(WriteTheseus(_folder),
            [Card(Git22, SessionLifecycle.Idle), Card("checker", role: WorkstreamRole.Verifier)], links);
        var verify = Box(view, "projects-and-method-ui").Stages.Single(stage => stage.Name == "Verify");

        Assert.That(Selected(view, verify).Rows.Single(row => row.Label == "Verifier").Value, Is.EqualTo("checker"));
    }

    [Test]
    public void TheLinks_SayWhichWayAndTheirState()
    {
        var view = _views.BuildYourMarket();

        Assert.That(Selected(view, Box(view, "bym-mcp-identity")).Links, Is.EqualTo(new[]
        {
            "buildyourmarket-mcp-server-spec hands work to this",
            "Hands work to identityserver-redirect-uri-anchoring-e1",
            "Reports back to Specify the BuildYourMarket MCP server: accepted",
        }));
        Assert.That(Selected(view, Box(view, ClientFixes)).Links, Is.EqualTo(new[]
        {
            "buildyourmarket-mcp-server-spec hands work to this", "Reports back to Specify the BuildYourMarket MCP server",
        }), "a link that names no document has no state to add");
    }

    [Test]
    public void ThenWorkedOn_IsALinkOfTheEarlierPiece()
    {
        var view = _views.Theseus();

        Assert.That(Selected(view, Box(view, "method-kit")).Links, Is.EqualTo(new[] { "Then worked on holdout-scenarios" }));
    }

    [Test]
    public void TheObjectiveSection_TicksCriteriaInTheFile_AndEditsTheObjective()
    {
        var view = _views.Theseus();
        var box = Box(view, "project-view");
        var pane = Selected(view, box);
        var line = box.Objective!.Criteria.Single().Line;

        Assert.That(pane.ObjectiveHeading, Is.EqualTo("Objective 1.1: Checks shown on cards"));
        Assert.That(pane.Criteria.Select(criterion => (criterion.Text, criterion.TickGlyph)), Is.EqualTo(new[] { ("A criterion.", "\uE739") }));

        pane.Criteria.Single().ToggleCommand!.Execute(null);
        pane.EditObjectiveCommand!.Execute(null);

        Assert.That(_views.Edited, Is.EqualTo(new[] { $"Tick:{line}:True", "Edit:1.1" }));
    }

    [Test]
    public void ATickedCriterion_ShowsItsTickAsAGlyph_AndUnticks()
    {
        var path = WriteTheseus(_folder).Folder + "\\project.md";
        File.WriteAllText(path, File.ReadAllText(path).Replace("- [ ] A criterion.", "- [x] A criterion.", StringComparison.Ordinal));
        var view = _views.Of(Read(_folder, TheseusProject), [Card(Git22, SessionLifecycle.Idle)]);
        var criterion = Selected(view, Box(view, "stop-timing")).Criteria.Single();

        criterion.ToggleCommand!.Execute(null);

        Assert.That(criterion.TickGlyph, Is.EqualTo("\uE73A"), "the icon font's ticked box (W25)");
        Assert.That(_views.Edited.Single(), Does.EndWith(":False"));
    }

    [Test]
    public void AHandIn_IsSaidOnTheBox_AndInFullInProperties()
    {
        // git-22, 2026-11-24: a lead gave an existing card work, and the view showed only the card turning busy.
        var at = new DateTimeOffset(2026, 11, 24, 8, 41, 0, TimeSpan.Zero);
        var handIn = new Theseus.Core.Projects.HandIn(at, "lead", "The lead", "Fix the search bugs\nget_contact returns 500 on an unknown id.");
        var view = _views.Of(WriteOne(_folder, "feature", Files("spec.md")), [Card(ACard, SessionLifecycle.Running) with { HandIn = handIn }]);
        var box = Box(view, APiece);
        var time = at.ToLocalTime().ToString("HH:mm", System.Globalization.CultureInfo.CurrentCulture);

        Assert.That(box.HandInText, Does.StartWith("← The lead: Fix the search bugs, ").And.EndWith(time));
        Assert.That(Selected(view, box).Rows.Single(row => row.Label == "Handed in").Value,
            Does.StartWith("By The lead, ").And.EndWith(": Fix the search bugs\nget_contact returns 500 on an unknown id."));
        Assert.That(Box(_views.Of(WriteOne(_folder, "feature", Files("spec.md")), [Card(ACard)]), APiece).HandInText, Is.Empty);
    }
}
