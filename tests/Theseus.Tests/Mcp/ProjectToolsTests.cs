using System.Text.Json.Nodes;
using Theseus.Core.Mcp;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;

namespace Theseus.Tests.Mcp;

/// <summary>
/// now_at and the tools that write project.md (1.1f E24, B9, E30a; brief, PR 2): each validates, refuses a stale file
/// with its reason, and writes only what the reader reads.
/// </summary>
[TestFixture]
public class ProjectToolsTests
{
    private const string Project = "p";

    private static readonly DateTimeOffset Now = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    private TestProjectsFolder _folder = null!;
    private StageReportBook _book = null!;
    private ProjectTools _tools = null!;
    private McpSession _session = null!;

    private static string Text =>
        TestProjectsFolder.ProjectText("P", body: "## Acceptance criteria\n\n- [ ] Overall.\n\n## Phase 0: Start\n\n"
            + "### Objective 0.1: Build it\n- [ ] One.\n- [ ] Two.\n\n**Pipeline:** feature\n"
            + "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n| card | build-it | owner/build-it | r |\n\n"
            + "### Objective 0.2: Look into it\n- [ ] Found.\n\n**Pipeline:** freeform\n"
            + "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n| other | look | none | r |\n");

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _folder.Write($"{Project}/project.md", Text);
        _book = new StageReportBook(Path.Combine(_folder.Folder, "stage-skips.json"));
        _session = new McpSession("s1", "card", "Card");
        _tools = new ProjectTools(key => ProjectMembers.From(ProjectCatalog.Read(_folder.Folder)).Of(key),
            name => ProjectCatalog.Read(_folder.Folder).SingleOrDefault(reading => reading.Name == name), _book, () => Now, TimeZoneInfo.Utc);
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private ProjectToolOutcome Call(string tool, JsonObject arguments, McpSession? session = null) =>
        _tools.Call(tool, session ?? _session, arguments);

    private ProjectFile File() => ProjectViewFixtures.Read(_folder, Project).File!;

    [Test]
    public void NowAt_RecordsTheStageAndWhatTheSessionIsDoing()
    {
        var outcome = Call("now_at", new JsonObject { ["stage"] = "Build", ["doing"] = "writing the reader" });

        Assert.That(outcome.IsError, Is.False, outcome.Text);
        Assert.That(_book.NowAtOf("card"), Is.EqualTo(new SessionStageReport(PipelineStage.Implement, "writing the reader", Now)));
    }

    [Test]
    public void NowAt_RefusesAStageThePieceDoesNotHave_NamingItsStages()
    {
        var outcome = Call("now_at", new JsonObject { ["stage"] = "Reproduce", ["doing"] = "x" });

        Assert.That(outcome.IsError, Is.False, "Reproduce names Research, which a feature has");
        Assert.That(Call("now_at", new JsonObject { ["stage"] = "Dance", ["doing"] = "x" }).Text,
            Does.Contain("Its stages are Research, Spec, Plan, Build, Verify, Review, Lessons"));
    }

    [Test]
    public void NowAt_TakesADoingUnder80Characters_AndRefuses80()
    {
        Assert.That(Call("now_at", new JsonObject { ["stage"] = "Plan", ["doing"] = new string('x', 80) }).IsError, Is.True);
        Assert.That(_book.NowAtOf("card"), Is.Null);
        Assert.That(Call("now_at", new JsonObject { ["stage"] = "Plan", ["doing"] = new string('x', 79) }).IsError, Is.False);
    }

    [TestCase("Spec")]
    [TestCase("Verify")]
    public void NowAt_RefusesASkipOfTheSpecGateOrTheCheck_AndKeepsNothing(string stage)
    {
        var outcome = Call("now_at", new JsonObject { ["stage"] = stage, ["doing"] = "skipped: not needed" });

        Assert.That(outcome.IsError, Is.True);
        Assert.That(outcome.Text, Does.Contain("cannot be skipped"));
        Assert.That(_book.SkipsOf(Project, "build-it"), Is.Empty);
    }

    [Test]
    public void TheToolsWrite_ThroughTheWriterTheyAreGiven()
    {
        var written = new List<string>();
        var tools = new ProjectTools(_ => new ProjectMember(Project, "build-it"), _ => ProjectViewFixtures.Read(_folder, Project), _book,
            () => Now, TimeZoneInfo.Utc, (project, edit) =>
            {
                written.Add(project);

                return edit(Text);
            });

        tools.Call("record_decision", _session, new JsonObject { ["decision"] = "x", ["by"] = "Nigel" });

        Assert.That(written, Is.EqualTo(new[] { Project }));
        Assert.That(System.IO.File.ReadAllText(_folder.Home(Project) + "/project.md"), Is.EqualTo(Text), "the writer, not the tool, writes");
    }

    [Test]
    public void AWriteThatFails_IsRefusedWithItsReason()
    {
        var tools = new ProjectTools(_ => new ProjectMember(Project, "build-it"), _ => ProjectViewFixtures.Read(_folder, Project), _book,
            () => Now, TimeZoneInfo.Utc, (_, _) => throw new IOException("the file is locked"));

        var outcome = tools.Call("record_decision", _session, new JsonObject { ["decision"] = "x", ["by"] = "Nigel" });

        Assert.That((outcome.IsError, outcome.Text), Is.EqualTo((true, "Refused: the file could not be written (the file is locked).")));
    }

    [Test]
    public void UntickingACriterion_TakesItsEvidenceAway_AndAWaitingNoteIsRefusedOnATick()
    {
        JsonObject Tick(bool ticked) => new()
        {
            ["objective"] = "0.1", ["criterion"] = 1, ["text"] = "One.", ["ticked"] = ticked, ["evidence"] = "review.md",
        };

        Call("tick_criterion", Tick(true));
        Assert.That(Call("set_waiting", new JsonObject
        {
            ["objective"] = "0.1", ["criterion"] = 1, ["text"] = "One.", ["who"] = "owner", ["what"] = "x",
        }).Text, Does.Contain("is ticked"));

        Call("tick_criterion", Tick(false));
        Assert.That(File().Objectives[0].Criteria[0].Evidence, Is.Null);
    }

    [Test]
    public void EditsAfterAFinalLineWithNoNewline_NeverJoinIt()
    {
        var bare = TestProjectsFolder.ProjectText("P", body: "## Phase 0: Start\n\n### Objective 0.1: Build it\n- [ ] Last.\n\n"
            + "**Pipeline:** feature\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n| card | build-it | owner/b | r |\n\n"
            + "## Decisions\n\n| Date | Decision | By |\n|---|---|---|\n| 2026-11-01 | a | Nigel |");

        var decided = ProjectFileEditor.AddDecision(bare, new DateOnly(2026, 11, 22), "b", "Ana");

        Assert.That(decided, Does.EndWith("| 2026-11-01 | a | Nigel |\n| 2026-11-22 | b | Ana |"));

        var criterionLast = bare[..bare.IndexOf("\n\n**Pipeline:**", StringComparison.Ordinal)];
        var noted = ProjectFileEditor.SetCriterionNote(criterionLast + "\n\n**Pipeline:** feature\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|",
            "0.1", 0, "Last.", CriterionWaiting.Label, "owner: x");

        Assert.That(ProjectFileReader.Read("p", noted).File!.Objectives[0].Criteria[0].Waiting, Is.EqualTo(new CriterionWaiting(true, "x")));
    }

    [Test]
    public void AddRound_PutsTheRoundLineUnderTheRealHeading_NotAFencedExample()
    {
        var text = TestProjectsFolder.ProjectText("P", body: "An example:\n\n```\n### Objective 0.1: Example\n```\n\n## Phase 0: Start\n\n"
            + "### Objective 0.1: Build it\n- [ ] One.\n\n**Pipeline:** feature\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n");

        var (edited, _) = ProjectFileEditor.AddRound(text, "Two", "why", "Nigel", new DateOnly(2026, 11, 22), ["0.1"]);

        Assert.That(edited, Does.Contain("```\n### Objective 0.1: Example\n```"));
        Assert.That(ProjectFileReader.Read("p", edited).File!.Objectives[0].Round, Is.EqualTo(2));
    }

    [Test]
    public void NowAt_ForAFreeformPiece_TakesOnlyWork()
    {
        var other = new McpSession("s2", "other", "Other");

        Assert.That(Call("now_at", new JsonObject { ["stage"] = "Work", ["doing"] = "inventory" }, other).IsError, Is.False);
        Assert.That(Call("now_at", new JsonObject { ["stage"] = "Plan", ["doing"] = "x" }, other).Text, Does.Contain("its one stage is Work"));
    }

    [Test]
    public void NowAt_Skipped_KeepsTheSkipWithItsReason_AcrossARestart()
    {
        Call("now_at", new JsonObject { ["stage"] = "Research", ["doing"] = "skipped: the 1.1d research holds" });

        var reopened = new StageReportBook(Path.Combine(_folder.Folder, "stage-skips.json"));

        Assert.That(reopened.SkipsOf(Project, "build-it"), Is.EquivalentTo(new Dictionary<PipelineStage, string>
        {
            [PipelineStage.Research] = "the 1.1d research holds",
        }));
        Assert.That(_book.NowAtOf("card"), Is.Null, "a skip is not what it is doing");
    }

    [Test]
    public void ACardInNoProject_IsRefused()
    {
        Assert.That(Call("now_at", new JsonObject { ["stage"] = "Plan", ["doing"] = "x" }, new McpSession("s3", "stranger", "S")).Text,
            Does.Contain("not in a project"));
    }

    [Test]
    public void RecordRound_MakesTheTableWithRound1_AndPutsARoundLineOnEachObjective()
    {
        var outcome = Call("record_round", new JsonObject
        {
            ["name"] = "GUI parity",
            ["why"] = "Built from tickets, not the GUI",
            ["objectives"] = new JsonArray("0.2"),
        });
        var file = File();

        Assert.That(outcome.IsError, Is.False, outcome.Text);
        Assert.That(file.RoundsTable.Select(round => (round.Number, round.Name, round.By)), Is.EqualTo(new[]
        {
            (1, "Round 1", "Nigel"), (2, "GUI parity", "Nigel"),
        }));
        Assert.That(file.RoundsTable[1].Began, Is.EqualTo(new DateOnly(2026, 11, 22)));
        Assert.That(file.Objectives.Select(objective => objective.Round), Is.EqualTo(new[] { 1, 2 }));
        Assert.That(System.IO.File.ReadAllText(_folder.Home(Project) + "/project.md"),
            Does.Contain("## Acceptance criteria\n\n- [ ] Overall.\n\n## Rounds\n"), "after the Acceptance criteria section (B6)");
    }

    [Test]
    public void RecordRound_ASecondTime_AddsARow()
    {
        Call("record_round", new JsonObject { ["name"] = "Two", ["why"] = "a", ["objectives"] = new JsonArray("0.2") });
        Call("record_round", new JsonObject { ["name"] = "Three", ["why"] = "b", ["objectives"] = new JsonArray("0.1"), ["by"] = "Ana" });
        var file = File();

        Assert.That(file.RoundsTable.Select(round => round.Name), Is.EqualTo(new[] { "Round 1", "Two", "Three" }));
        Assert.That(file.RoundsTable[2].By, Is.EqualTo("Ana"));
        Assert.That(file.Objectives.Select(objective => objective.Round), Is.EqualTo(new[] { 3, 2 }));
    }

    [Test]
    public void RecordRound_RefusesAnObjectiveTheFileDoesNotHave_AndWritesNothing()
    {
        var outcome = Call("record_round", new JsonObject { ["name"] = "X", ["why"] = "y", ["objectives"] = new JsonArray("9.9") });

        Assert.That(outcome.IsError, Is.True);
        Assert.That(System.IO.File.ReadAllText(_folder.Home(Project) + "/project.md"), Is.EqualTo(Text));
    }

    [Test]
    public void AnEdit_IsRefused_WhenTheFileChangedSinceTheseusReadIt()
    {
        var stale = ProjectViewFixtures.Read(_folder, Project);
        var tools = new ProjectTools(_ => new ProjectMember(Project, "build-it"), _ => stale, _book, () => Now, TimeZoneInfo.Utc);
        _folder.Write($"{Project}/project.md", Text + "\nA note.\n");

        var outcome = tools.Call("record_decision", _session, new JsonObject { ["decision"] = "x", ["by"] = "Nigel" });

        Assert.That(outcome.IsError, Is.True);
        Assert.That(outcome.Text, Does.Contain("changed since Theseus read it"));
    }

    [Test]
    public void TickCriterion_TicksIt_AndKeepsTheEvidenceBesideIt()
    {
        var outcome = Call("tick_criterion", new JsonObject
        {
            ["objective"] = "0.1",
            ["criterion"] = 2,
            ["text"] = "Two.",
            ["evidence"] = "evidence.md: 33 of 33 passed",
        });
        var criterion = File().Objectives[0].Criteria[1];

        Assert.That(outcome.IsError, Is.False, outcome.Text);
        Assert.That((criterion.IsTicked, criterion.Text, criterion.Evidence), Is.EqualTo((true, "Two.", "evidence.md: 33 of 33 passed")));
    }

    [Test]
    public void TickCriterion_RefusesATickWithNoEvidence_AndTextThatNoLongerMatches()
    {
        Assert.That(Call("tick_criterion", new JsonObject { ["objective"] = "0.1", ["criterion"] = 1, ["text"] = "One." }).Text,
            Does.Contain("needs its evidence"));
        Assert.That(Call("tick_criterion", new JsonObject
        {
            ["objective"] = "0.1", ["criterion"] = 1, ["text"] = "Something else.", ["evidence"] = "x",
        }).Text, Does.Contain("no longer reads"));
    }

    [Test]
    public void AddObjective_AddsItWithItsPipelineCriteriaAndWaitingNotes()
    {
        var outcome = Call("add_objective", new JsonObject
        {
            ["phase"] = 0,
            ["title"] = "Ship it",
            ["criteria"] = new JsonArray("Released.", "Announced."),
            ["pipeline"] = "feature",
            ["waiting"] = new JsonArray("owner: approve the release", ""),
        });
        var added = File().Objectives[^1];

        Assert.That(outcome.Text, Is.EqualTo("Added objective 0.3: Ship it."));
        Assert.That((added.Number, added.Title, added.Pipelines.Single()), Is.EqualTo(("0.3", "Ship it", PipelineKind.Feature)));
        Assert.That(added.Criteria.Select(criterion => criterion.Waiting), Is.EqualTo(new[]
        {
            new CriterionWaiting(true, "approve the release"), null,
        }));
    }

    [Test]
    public void RecordDecision_AppendsADatedRow_MakingTheTableWhenThereIsNone()
    {
        Call("record_decision", new JsonObject { ["decision"] = "Rounds are recorded by tool", ["by"] = "Nigel" });
        Call("record_decision", new JsonObject { ["decision"] = "A second | with a bar", ["by"] = "Ana" });
        var text = System.IO.File.ReadAllText(_folder.Home(Project) + "/project.md");

        Assert.That(text, Does.EndWith("## Decisions\n\n| Date | Decision | By |\n|---|---|---|\n"
                                       + "| 2026-11-22 | Rounds are recorded by tool | Nigel |\n| 2026-11-22 | A second / with a bar | Ana |\n"));
        Assert.That(File().FirstDecision, Is.EqualTo(new DateOnly(2026, 11, 22)));
    }

    [Test]
    public void SetWaiting_WritesTheOwnerForm_ReplacesIt_AndRemovesIt()
    {
        JsonObject Args(string who, string? what) => new()
        {
            ["objective"] = "0.1", ["criterion"] = 1, ["text"] = "One.", ["who"] = who, ["what"] = what,
        };

        Call("set_waiting", Args("owner", "pull request 42 is held for your review"));
        Assert.That(File().Objectives[0].Criteria[0].Waiting, Is.EqualTo(new CriterionWaiting(true, "pull request 42 is held for your review")));
        Assert.That(File().Objectives[0].Criteria[0].WaitsOnOwner, Is.True);

        Call("set_waiting", Args("work", "run the checks in QA"));
        Assert.That(File().Objectives[0].Criteria[0].Waiting, Is.EqualTo(new CriterionWaiting(false, "run the checks in QA")));
        Assert.That(System.IO.File.ReadAllText(_folder.Home(Project) + "/project.md"), Does.Contain("- [ ] One.\n      Waiting: work: run the checks in QA\n- [ ] Two."));

        Call("set_waiting", Args("none", null));
        Assert.That(File().Objectives[0].Criteria[0].Waiting, Is.Null);
        Assert.That(File().Objectives[0].Criteria[0].Text, Is.EqualTo("One."));
    }

    [Test]
    public void TheReader_KeepsWaitingAndEvidenceLinesOutOfTheCriterionsText()
    {
        var file = ProjectFileReader.Read("x", TestProjectsFolder.ProjectText("X", body: "## Phase 0: S\n\n### Objective 0.1: O\n"
            + "- [ ] A long criterion\n      that wraps.\n      Waiting: owner: name the pilot users\n- [x] Done.\n      Evidence: review.md\n"
            + "- [ ] No note.\n\n**Pipeline:** freeform\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n")).File!;
        var criteria = file.Objectives[0].Criteria;

        Assert.That(criteria.Select(criterion => criterion.Text), Is.EqualTo(new[] { "A long criterion that wraps.", "Done.", "No note." }));
        Assert.That(criteria[0].Waiting, Is.EqualTo(new CriterionWaiting(true, "name the pilot users")));
        Assert.That(criteria[1].Evidence, Is.EqualTo("review.md"));
        Assert.That(criteria.Select(criterion => criterion.WaitsOnOwner), Is.EqualTo(new[] { true, false, false }), "no note waits on work (G37b)");
    }

    [Test]
    public void TheToolsAreOffered_AndOnlyTheOwnersActsAskInTheTab()
    {
        Assert.That(ProjectTools.Names, Is.SubsetOf(TheseusMcp.ToolNames));
        Assert.That(ProjectTools.Names.Where(TheseusMcp.PreApproved.Contains),
            Is.EqualTo(new[] { "now_at", "tick_criterion", "record_decision", "set_waiting" }), "decision G9");
        Assert.That(McpHandler.Instructions, Does.Contain("now_at(stage, doing)"));
    }
}
