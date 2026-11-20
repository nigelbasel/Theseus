using System.Runtime.CompilerServices;
using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// The project file's shape, method-v0.2 (spec AC 3): a test per shape the reader accepts and per refusal. A file that
/// does not parse is unreadable with its line and reason, never read as empty or guessed.
/// </summary>
[TestFixture]
public class ProjectFileReaderTests
{
    private const string Header = "# A project\n\n**Kit:** method-v0.2\n**Primary branch:** owner/project\n\n";

    private const string Table = "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n";

    private static ProjectFileReading Read(string body, string header = Header) =>
        ProjectFileReader.Read("a-project", header + body);

    private static ProjectFile File(string body) =>
        Read(body).File ?? throw new AssertionException($"unreadable: {Read(body).Reason}");

    private static string Objective(string number = "0.1", string criteria = "- [ ] One.\n",
        string pipeline = "**Pipeline:** feature\n", string table = Table + "| A workstream | a-slug | owner/a-slug | Repo |\n") =>
        $"### Objective {number}: Something\n{criteria}\n{pipeline}{table}\n";

    [Test]
    public void AFileWithNoPhases_ReadsItsTitleKitAndPrimaryBranch()
    {
        var file = File("## Objective\nThe outcome.\n");

        Assert.That((file.Name, file.Title, file.Kit, file.PrimaryBranch),
            Is.EqualTo(("a-project", "A project", "method-v0.2", "owner/project")));
    }

    [Test]
    public void APhase_ReadsItsNumberNameAndObjectives()
    {
        var file = File("## Phase 0: Seed\n**Exit gate:** the owner accepts.\n\n" + Objective());

        Assert.That(file.Phases.Single().Number, Is.EqualTo(0));
        Assert.That(file.Phases.Single().Name, Is.EqualTo("Seed"));
        Assert.That(file.Phases.Single().Objectives.Single().Number, Is.EqualTo("0.1"));
        Assert.That(file.Phases.Single().Objectives.Single().Title, Is.EqualTo("Something"));
    }

    [Test]
    public void Criteria_AreReadTickedAndNot_WithTheirContinuationLines()
    {
        var file = File("## Phase 0: Seed\n" + Objective(criteria: "- [x] Done one.\n- [ ] Open one,\n      carried on.\n- [X] Also done.\n"));

        var criteria = file.Objectives.Single().Criteria;

        Assert.That(criteria.Select(criterion => (criterion.Text, criterion.IsTicked)), Is.EqualTo(new[]
        {
            ("Done one.", true),
            ("Open one, carried on.", false),
            ("Also done.", true),
        }));
    }

    [Test]
    public void APlainBulletOrParagraph_AmongTheCriteria_IsProse()
    {
        var file = File("## Phase 0: Seed\n" + Objective(criteria: "What it must achieve.\n- [ ] One.\n- Settled: something,\n  carried on.\n- [ ] Two.\n"));

        Assert.That(file.Objectives.Single().Criteria.Select(criterion => criterion.Text), Is.EqualTo(new[] { "One.", "Two." }));
    }

    [Test]
    public void ThePipelineAndTheTable_AreRead()
    {
        var file = File("## Phase 0: Seed\n" + Objective(table: Table
            + "| A workstream | first-slug | owner/first-slug | Repo, Other |\n| A workstream | second-slug | main | Repo |\n"));

        var objective = file.Objectives.Single();

        Assert.That(objective.Pipelines, Is.EqualTo(new[] { PipelineKind.Feature }));
        Assert.That(objective.Workstreams.Select(row => (row.Workstream, row.Slug, row.Branch, row.Repos)), Is.EqualTo(new[]
        {
            ("A workstream", "first-slug", "owner/first-slug", "Repo, Other"),
            ("A workstream", "second-slug", "main", "Repo"),
        }));
    }

    [TestCase("bug", PipelineKind.Bug)]
    [TestCase("freeform", PipelineKind.Freeform)]
    [TestCase("Feature", PipelineKind.Feature)]
    public void EachPipeline_IsRead(string written, PipelineKind expected)
    {
        var file = File("## Phase 0: Seed\n" + Objective(pipeline: $"**Pipeline:** {written}\n"));

        Assert.That(file.Objectives.Single().Pipelines, Is.EqualTo(new[] { expected }));
    }

    [Test]
    public void TwoPipelines_AreBothRead()
    {
        var file = File("## Phase 0: Seed\n" + Objective(pipeline: "**Pipeline:** feature or bug\n"));

        Assert.That(file.Objectives.Single().Pipelines, Is.EqualTo(new[] { PipelineKind.Feature, PipelineKind.Bug }));
    }

    [Test]
    public void APipelineToBeChosen_ReadsAsNoneYet_WithAnEmptyTable()
    {
        var file = File("## Phase 0: Seed\n" + Objective(pipeline: "**Pipeline:** [to be chosen]\n", table: Table));

        Assert.That(file.Objectives.Single().Pipelines, Is.Empty);
        Assert.That(file.Objectives.Single().Workstreams, Is.Empty);
    }

    [Test]
    public void SeveralExitGates_AreJoinedInOrder_WithTheirContinuationLines()
    {
        var file = File("## Phase 0: Seed\n**Exit gate:** first part,\ncarried on.\n\n" + Objective("0.1")
                        + "**Exit gate:** second part.\n\n**Changes go through pull requests.** Prose.\n");

        Assert.That(file.Phases.Single().ExitGate, Is.EqualTo("first part, carried on. second part."));
    }

    [Test]
    public void AHeadingInsideAFencedBlock_IsNotRead()
    {
        var file = File("## Phase 0: Seed\n" + Objective() + "```markdown\n### Objective 9.9: an example\n```\n");

        Assert.That(file.Objectives.Select(objective => objective.Number), Is.EqualTo(new[] { "0.1" }));
    }

    [Test]
    public void OtherSections_EndAPhase_AndTheirCriteriaAreNotAnObjectives()
    {
        var file = File("## Acceptance criteria\n- [ ] The project's own.\n\n## Phase 0: Seed\n" + Objective()
                        + "## Decisions\n| Date | Decision | By |\n|---|---|---|\n");

        Assert.That(file.Objectives.Single().Criteria.Select(criterion => criterion.Text), Is.EqualTo(new[] { "One." }));
    }

    [Test]
    public void AnObjective_IsDone_OnlyWhenEveryCriterionIsTicked()
    {
        var file = File("## Phase 0: Seed\n" + Objective("0.1", "- [x] One.\n- [x] Two.\n", table: Table)
                        + Objective("0.2", "- [x] One.\n- [ ] Two.\n", table: Table)
                        + Objective("0.3", "[criteria to be written]\n", table: Table));

        Assert.That(file.Objectives.Select(objective => objective.IsDone), Is.EqualTo(new[] { true, false, false }),
            "an objective whose criteria are not written yet is not done");
        Assert.That(file.ObjectivesDone, Is.EqualTo(1));
    }

    [Test]
    public void TheCurrentPhase_IsTheFirstWithAnObjectiveNotDone()
    {
        var file = File("## Phase 0: Seed\n" + Objective("0.1", "- [x] One.\n", table: Table)
                        + "## Phase 1: Evidence\n" + Objective("1.1", "- [ ] One.\n", table: Table)
                        + "## Phase 2: Later\n" + Objective("2.1", "- [ ] One.\n", table: Table));

        Assert.That(file.CurrentPhase!.Number, Is.EqualTo(1));
    }

    [Test]
    public void WhenEveryObjectiveIsDone_ThereIsNoCurrentPhase()
    {
        var file = File("## Phase 0: Seed\n" + Objective("0.1", "- [x] One.\n", table: Table));

        Assert.That(file.CurrentPhase, Is.Null);
    }

    [Test]
    public void TheStarterProjectFile_IsReadable_AsANewProjectGetsIt()
    {
        var path = Path.Combine(RepositoryRoot(), "method", "starter", "project.md");
        var reading = ProjectFileReader.Read("a-project", System.IO.File.ReadAllText(path));

        Assert.That(reading.Reason, Is.Null, "a new project starts readable (1.1b AC 7 and 8)");
        Assert.That(reading.File!.Kit, Is.EqualTo(MethodKits.Current));
        Assert.That(reading.File.Objectives.Single().Workstreams, Is.Empty);
    }

    [Test]
    public void AFileAtMethodV01_IsPinned_AndNotReadFurther()
    {
        var reading = Read("## Phase 0: anything at all\n- not the shape\n", "# A project\n\n**Kit:** method-v0.1\n\n");

        Assert.That(reading.Status, Is.EqualTo(ProjectReadingStatus.Pinned));
        Assert.That(reading.Describe, Is.EqualTo("a-project: pinned to method-v0.1"));
    }

    private static readonly object[] Refusals =
    [
        new object[] { "\n\nNo title here.\n", "", "line 3: the file must start with a '# <title>' heading" },
        new object[] { "", "# A project\n\n**Primary branch:** owner/project\n\n", "line 1: no **Kit:** line under the title" },
        new object[] { "", "# A project\n\n**Kit:** method-v9.9\n**Primary branch:** owner/project\n", "line 3: kit method-v9.9 is not one this Theseus reads (it reads method-v0.2)" },
        new object[] { "", "# A project\n\n**Kit:** method-v0.2\n\n", "line 1: no **Primary branch:** line under the title" },
        new object[] { "## Phase one: Seed\n", Header, "line 6: a phase heading is '## Phase <n>: <name>'" },
        new object[] { "## Phase 1: Seed\n", Header, "line 6: phase 1 is out of order: phases are numbered from 0, one after another" },
        new object[] { "## Phase 0: Seed\n### Objective one: Something\n", Header, "line 7: an objective heading is '### Objective <n>.<m>: <title>'" },
        new object[] { "### Objective 0.1: Something\n", Header, "line 6: objective 0.1 is not under a '## Phase' heading" },
        new object[] { "## Phase 0: Seed\n" + Objective("1.1"), Header, "line 7: objective 1.1 is numbered for phase 1 but sits in phase 0" },
        new object[] { "## Phase 0: Seed\n" + Objective("0.1", table: Table) + Objective("0.1", table: Table), Header, "line 14: objective 0.1 appears twice" },
        new object[] { "## Phase 0: Seed\n" + Objective(pipeline: "", table: ""), Header, "line 7: objective 0.1 has no **Pipeline:** line" },
        new object[] { "## Phase 0: Seed\n" + Objective(pipeline: "**Pipeline:** spike\n"), Header, "line 10: pipeline 'spike' is not feature, bug or freeform" },
        new object[] { "## Phase 0: Seed\n" + Objective(pipeline: "**Pipeline:** feature\n**Pipeline:** bug\n"), Header, "line 11: objective 0.1 has a second **Pipeline:** line" },
        new object[] { "## Phase 0: Seed\n" + Objective(table: ""), Header, "line 7: objective 0.1 has no workstreams table '| Workstream | Slug | Branch | Repos |'" },
        new object[] { "## Phase 0: Seed\n### Objective 0.1: Something\n" + Table + "\n**Pipeline:** feature\n", Header, "line 8: the workstreams table comes after the **Pipeline:** line" },
        new object[] { "## Phase 0: Seed\n" + Objective(table: "| Slug | Change | Pipeline |\n|---|---|---|\n"), Header, "line 11: the table under objective 0.1 is not '| Workstream | Slug | Branch | Repos |'" },
        new object[] { "## Phase 0: Seed\n" + Objective(table: Table + "\n" + Table), Header, "line 14: objective 0.1 has a second table" },
        new object[] { "## Phase 0: Seed\n" + Objective(table: "| Workstream | Slug | Branch | Repos |\n| A | a-slug | main | Repo |\n"), Header, "line 12: the workstreams table's second line must be its separator, '|---|---|---|---|'" },
        new object[] { "## Phase 0: Seed\n" + Objective(table: Table + "| A | a-slug | main |\n"), Header, "line 13: a workstreams row has 3 cells, not 4" },
        new object[] { "## Phase 0: Seed\n" + Objective(table: Table + "| A | A Slug | main | Repo |\n"), Header, "line 13: slug 'A Slug' is not lower-case letters, digits and hyphens" },
        new object[] { "## Phase 0: Seed\n" + Objective(table: Table + "|  | a-slug | main | Repo |\n"), Header, "line 13: a workstreams row has no Workstream" },
        new object[] { "## Phase 0: Seed\n" + Objective(table: Table + "| A | a-slug |  | Repo |\n"), Header, "line 13: a workstreams row has no Branch" },
        new object[] { "## Phase 0: Seed\n" + Objective(table: Table + "| A | a-slug | main |  |\n"), Header, "line 13: a workstreams row has no Repos" },
        new object[] { "## Phase 0: Seed\n" + Objective("0.1") + Objective("0.2"), Header, "line 21: slug 'a-slug' is listed twice (first at line 13)" },
        new object[] { "## Phase 0: Seed\n- [ ] A criterion with no objective.\n", Header, "line 7: a criterion outside an objective" },
    ];

    [TestCaseSource(nameof(Refusals))]
    public void AFileOutOfShape_IsUnreadable_WithItsLineAndReason(string body, string header, string reason)
    {
        var reading = Read(body, header);

        Assert.That(reading.Status, Is.EqualTo(ProjectReadingStatus.Unreadable));
        Assert.That(reading.File, Is.Null, "never read as empty or guessed");
        Assert.That(reading.Reason, Is.EqualTo(reason));
    }

    [Test]
    public void AnUnreadableFile_IsDescribedAsTheSpecWordsIt()
    {
        var reading = Read("", "# A project\n\n**Kit:** method-v0.2\n\n");

        Assert.That(reading.Describe, Is.EqualTo("a-project: unreadable: line 1: no **Primary branch:** line under the title"));
    }

    [Test]
    public void TheBeforeTheKitLine_NamesSlugs_AndAFileWithoutOneNamesNone()
    {
        var named = File("## Objective\nThe outcome.\n");
        var listed = Read("", Header.TrimEnd('\n') + "\n**Before method-v0.2:** old-one, `old-two`\n\n").File!;

        Assert.That(named.BeforeKit, Is.Empty);
        Assert.That(listed.BeforeKit, Is.EquivalentTo(new[] { "old-one", "old-two" }));
    }

    private static string RepositoryRoot([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "..", ".."));
}
