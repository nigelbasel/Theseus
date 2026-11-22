using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>The Rounds table, the Round line and the What column, and the reader rule for files without them (1.1f B6 to B8).</summary>
[TestFixture]
public class ProjectRoundsReaderTests
{
    private const string Rounds = "## Rounds\n\n| Round | Name | Began | Why | By |\n|---|---|---|---|---|\n"
                                  + "| 1 | Specify | 2026-11-18 | The first requirements | Nigel |\n"
                                  + "| 2 | GUI parity | 2026-11-21 | Built from tickets, not the GUI | Nigel |\n\n";

    private static string Objective(string number, string round = "", string header = "| Workstream | Slug | Branch | Repos |",
        string rows = "") =>
        $"### Objective {number}: Something\n{round}- [ ] A criterion.\n\n**Pipeline:** feature\n{header}\n"
        + $"|{string.Concat(Enumerable.Repeat("---|", header.Count(ch => ch == '|') - 1))}\n{rows}\n";

    private static ProjectFileReading Read(string body) =>
        ProjectFileReader.Read("p", TestProjectsFolder.ProjectText("P", body: body));

    [Test]
    public void ARoundsTable_IsRead_InOrder()
    {
        var file = Read(Rounds + "## Phase 0: Start\n\n" + Objective("0.1")).File!;

        Assert.That(file.Rounds.Select(round => (round.Number, round.Name, round.Began, round.By)), Is.EqualTo(new[]
        {
            (1, "Specify", (DateOnly?)new DateOnly(2026, 11, 18), "Nigel"),
            (2, "GUI parity", (DateOnly?)new DateOnly(2026, 11, 21), "Nigel"),
        }));
        Assert.That(file.Rounds[1].Why, Is.EqualTo("Built from tickets, not the GUI"));
        Assert.That(file.Rounds, Has.None.Matches<ProjectRound>(round => round.IsImplicit));
    }

    [Test]
    public void ARoundLine_PutsItsObjectiveInThatRound_AndNoLineMeansRound1()
    {
        var file = Read(Rounds + "## Phase 0: Start\n\n" + Objective("0.1") + Objective("0.2", "**Round:** 2\n")).File!;

        Assert.That(file.Objectives.Select(objective => objective.Round), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void ARoundLineNamingARoundNotInTheTable_IsRefused_WithItsLine()
    {
        var reading = Read(Rounds + "## Phase 0: Start\n\n" + Objective("0.1", "**Round:** 3\n"));

        Assert.That(reading.IsReadable, Is.False);
        Assert.That(reading.Reason, Is.EqualTo("line 16: objective 0.1 names round 3, which is not in the Rounds table"));
    }

    [Test]
    public void ARoundLineInAFileWithNoTable_IsRefused_UnlessItIsRound1()
    {
        Assert.That(Read("## Phase 0: Start\n\n" + Objective("0.1", "**Round:** 2\n")).Reason,
            Does.EndWith("objective 0.1 names round 2, but the file has no Rounds table"));
        Assert.That(Read("## Phase 0: Start\n\n" + Objective("0.1", "**Round:** 1\n")).IsReadable, Is.True);
    }

    [TestCase("## Rounds\n\n| Round | Name |\n|---|---|\n| 1 | A |\n\n", "the Rounds table is not")]
    [TestCase("## Rounds\n\n| Round | Name | Began | Why | By |\n|---|---|---|---|---|\n| 2 | A | 2026-11-18 | x | y |\n\n", "out of order")]
    [TestCase("## Rounds\n\n| Round | Name | Began | Why | By |\n|---|---|---|---|---|\n| 1 | A | 18 Nov | x | y |\n\n", "is not a date")]
    [TestCase("## Rounds\n\n| Round | Name | Began | Why | By |\n|---|---|---|---|---|\n\n", "has no rows")]
    public void ARoundsTableThatDoesNotParse_MakesTheFileNeedUpdating_WithItsLine(string rounds, string reason)
    {
        var reading = Read(rounds + "## Phase 0: Start\n\n" + Objective("0.1"));

        Assert.That(reading.IsReadable, Is.False);
        Assert.That(reading.Reason, Does.StartWith("line ").And.Contain(reason));
    }

    [Test]
    public void AFileWithNoRoundsTable_ReadsAsRound1_BegunOnItsFirstDecision()
    {
        var file = Read("Chosen 2026-11-10.\n\n## Phase 0: Start\n\n" + Objective("0.1")
                        + "## Decisions\n\n| Date | Decision | By |\n|---|---|---|\n| 2026-11-18 | One | Nigel |\n| 2026-11-19 | Two | Nigel |\n").File!;
        var round = file.Rounds.Single();

        Assert.That((round.Number, round.Name, round.IsImplicit, round.Began), Is.EqualTo((1, "Round 1", true, (DateOnly?)new DateOnly(2026, 11, 18))));
    }

    [Test]
    public void WithNoDecisionsEither_ItBeginsOnTheEarliestDateItNames_AndOtherwiseOnNoDate()
    {
        Assert.That(Read("Later 2026-11-21, first 2026-11-19.\n\n## Phase 0: Start\n\n" + Objective("0.1")).File!.Rounds.Single().Began,
            Is.EqualTo(new DateOnly(2026, 11, 19)));
        Assert.That(Read("## Phase 0: Start\n\n" + Objective("0.1")).File!.Rounds.Single().Began, Is.Null);
    }

    [Test]
    public void AWhatColumn_IsReadAsTheRowsWhat_AndAnEmptyCellIsNone()
    {
        var file = Read("## Phase 0: Start\n\n" + Objective("0.1", header: "| Workstream | Slug | Branch | Repos | What |",
            rows: "| a | one | b/one | r | Reads from tables |\n| a | two | b/two | r |  |\n")).File!;

        Assert.That(file.Workstreams.Select(row => row.What), Is.EqualTo(new[] { "Reads from tables", null }));
    }

    [Test]
    public void AFourColumnTable_StillReads_WithNoWhat()
    {
        var file = Read("## Phase 0: Start\n\n" + Objective("0.1", rows: "| a | one | b/one | r |\n")).File!;

        Assert.That(file.Workstreams.Single().What, Is.Null);
    }

    [Test]
    public void ARowWithTooFewCellsForAWhatTable_IsRefused()
    {
        Assert.That(Read("## Phase 0: Start\n\n" + Objective("0.1", header: "| Workstream | Slug | Branch | Repos | What |",
            rows: "| a | one | b/one | r |\n")).Reason, Does.EndWith("a workstreams row has 4 cells, not 5"));
    }

    [Test]
    public void AddRow_IntoAWhatTable_WritesTheFifthCell()
    {
        var text = TestProjectsFolder.ProjectText("P", body: "## Phase 0: Start\n\n"
            + Objective("0.1", header: "| Workstream | Slug | Branch | Repos | What |", rows: "| a | one | b/one | r | One |\n"));

        var edited = ProjectFileEditor.AddRow(text, "0.1", new ProjectWorkstreamRow("c", "two", "b/two", "r", 0) { What = "Two" });

        Assert.That(ProjectFileReader.Read("p", edited).File!.Workstreams.Select(row => row.What), Is.EqualTo(new[] { "One", "Two" }));
    }
}
