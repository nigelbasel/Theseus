using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// The owner's edits to a project file are each the smallest change: the line they add, remove or change, and every
/// other byte as it was, line endings included (1.1c AC 9). An edit that cannot be made is refused with its reason.
/// </summary>
[TestFixture]
public class ProjectFileEditorTests
{
    private const string File =
        "# A project\n\n**Kit:** method-v0.2\n**Primary branch:** owner/project\n\n" +
        "Prose a session wrote, left alone.\n\n" +
        "## Phase 0: Start\n\n**Exit gate:** the owner says so.\n\n" +
        "### Objective 0.1: First\n\n- [ ] One.\n- [x] Two.\n\n**Pipeline:** feature\n" +
        "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n| git-22 | first-slug | owner/first-slug | repo |\n\n" +
        "### Objective 0.2: Second\n\n- [ ] Three.\n\n**Pipeline:** [to be chosen]\n" +
        "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n\n" +
        "## Phase 1: Next\n\n### Objective 1.1: Later\n\n- [ ] Four.\n\n**Pipeline:** feature\n" +
        "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n\n" +
        "## Decisions\n\n| Date | Decision | By |\n|---|---|---|\n";

    private static readonly ProjectWorkstreamRow NewRow = new("other-card", "new-slug", "owner/new-slug", "repo", 0);

    private static ProjectFile Read(string text) =>
        ProjectFileReader.Read("a-project", text).File ?? throw new AssertionException("the edit left the file unreadable");

    // The edited text with one line taken out is the original, so nothing else moved.
    private static void AssertOnlyAdded(string before, string after, string line)
    {
        var lines = after.Split('\n').ToList();
        Assert.That(lines.Remove(line), Is.True, $"'{line}' is not in the edited file");
        Assert.That(string.Join('\n', lines), Is.EqualTo(before));
    }

    [Test]
    public void AddingARow_PutsItAtTheEndOfTheObjectivesTable_AndChangesNothingElse()
    {
        var edited = ProjectFileEditor.AddRow(File, "0.1", NewRow);

        AssertOnlyAdded(File, edited, "| other-card | new-slug | owner/new-slug | repo |");
        Assert.That(Read(edited).ObjectiveOf("new-slug")!.Number, Is.EqualTo("0.1"));
    }

    [Test]
    public void AddingARow_ToAnEmptyTable_PutsItUnderTheSeparator()
    {
        var edited = ProjectFileEditor.AddRow(File, "0.2", NewRow);

        AssertOnlyAdded(File, edited, "| other-card | new-slug | owner/new-slug | repo |");
        Assert.That(Read(edited).ObjectiveOf("new-slug")!.Number, Is.EqualTo("0.2"));
    }

    [Test]
    public void RemovingACard_TakesOutItsRows_AndNothingElse()
    {
        var added = ProjectFileEditor.AddRow(File, "0.1", NewRow);

        Assert.That(ProjectFileEditor.RemoveRows(added, "other-card"), Is.EqualTo(File));
        Assert.Throws<ProjectEditRefusedException>(() => ProjectFileEditor.RemoveRows(File, "not-named"));
    }

    [Test]
    public void TickingACriterion_ChangesOnlyItsBox()
    {
        var one = Read(File).Objectives[0].Criteria[0];

        var ticked = ProjectFileEditor.SetCriterion(File, one.Line, ticked: true);

        Assert.That(ticked, Is.EqualTo(File.Replace("- [ ] One.", "- [x] One.", StringComparison.Ordinal)));
        Assert.That(ProjectFileEditor.SetCriterion(ticked, one.Line, ticked: false), Is.EqualTo(File));
        Assert.Throws<ProjectEditRefusedException>(() => ProjectFileEditor.SetCriterion(File, 1, ticked: true), "the title is not a criterion");
    }

    [Test]
    public void RenamingAnObjective_KeepsItsNumber()
    {
        var renamed = ProjectFileEditor.RenameObjective(File, "0.2", "Second, renamed");

        Assert.That(renamed, Is.EqualTo(File.Replace("### Objective 0.2: Second\n", "### Objective 0.2: Second, renamed\n", StringComparison.Ordinal)));
    }

    [Test]
    public void AddingAnObjective_NumbersItAfterThePhasesLast_AtThePhasesEnd()
    {
        var (edited, number) = ProjectFileEditor.AddObjective(File, 0, "Third", ["Five.", " "]);
        var file = Read(edited);

        Assert.That(number, Is.EqualTo("0.3"));
        Assert.That(file.Phases[0].Objectives.Select(objective => objective.Number), Is.EqualTo(new[] { "0.1", "0.2", "0.3" }));
        Assert.That(file.Phases[0].Objectives[2].Criteria.Single().Text, Is.EqualTo("Five."));
        Assert.That(file.Phases[1].Objectives.Single().Number, Is.EqualTo("1.1"), "the next phase is untouched");
        Assert.That(edited, Does.StartWith(File[..File.IndexOf("## Phase 1", StringComparison.Ordinal)].TrimEnd('\n')));
    }

    [Test]
    public void WindowsLineEndings_AreKept()
    {
        var crlf = File.Replace("\n", "\r\n", StringComparison.Ordinal);

        var edited = ProjectFileEditor.AddRow(crlf, "0.1", NewRow);

        Assert.That(edited.Replace("\r\n", "\n", StringComparison.Ordinal), Is.EqualTo(ProjectFileEditor.AddRow(File, "0.1", NewRow)));
        Assert.That(edited.Split("\r\n").Length, Is.EqualTo(edited.Split('\n').Length), "no bare line feed was added");
    }

    [Test]
    public void AnEditToAnObjectiveThatIsNotThere_OrAFileThatDoesNotRead_IsRefused()
    {
        Assert.That(Assert.Throws<ProjectEditRefusedException>(() => ProjectFileEditor.AddRow(File, "9.9", NewRow))!.Message,
            Is.EqualTo("there is no objective 9.9"));
        Assert.That(Assert.Throws<ProjectEditRefusedException>(() => ProjectFileEditor.AddRow("no title", "0.1", NewRow))!.Message,
            Does.StartWith("the file does not read"));
        Assert.Throws<ProjectEditRefusedException>(() => ProjectFileEditor.RenameObjective(File, "0.1", "two\nlines"));
    }

    [Test]
    public void ARowWithASlugTheFileHasAlready_IsRefused_RatherThanBreakingTheFile()
    {
        var taken = NewRow with { Slug = "first-slug" };

        Assert.That(Assert.Throws<ProjectEditRefusedException>(() => ProjectFileEditor.AddRow(File, "0.2", taken))!.Message,
            Is.EqualTo("the slug first-slug is already in project's table, for git-22"));
    }

    [Test]
    public void InAFileWithMixedLineEndings_EditsLandOnTheLineTheReaderCounted_AndEachLineKeepsItsEnding()
    {
        // As a CRLF file looks after a session wrote one line with a bare line feed.
        var mixed = File.Replace("\n", "\r\n", StringComparison.Ordinal)
            .Replace("- [ ] One.\r\n", "- [ ] One.\n", StringComparison.Ordinal);
        var two = Read(mixed).Objectives[0].Criteria[1];

        var ticked = ProjectFileEditor.SetCriterion(mixed, two.Line, ticked: false);

        Assert.That(ticked, Is.EqualTo(mixed.Replace("- [x] Two.", "- [ ] Two.", StringComparison.Ordinal)));

        var removed = ProjectFileEditor.RemoveRows(ProjectFileEditor.AddRow(mixed, "0.1", NewRow), "other-card");

        Assert.That(removed, Is.EqualTo(mixed));
    }

    [Test]
    public void ATableHeaderWrittenWithOtherSpacing_IsStillFound()
    {
        var spaced = File.Replace("| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n| git-22",
            "|Workstream|Slug|Branch|Repos|\n|---|---|---|---|\n| git-22", StringComparison.Ordinal);
        Assume.That(Read(spaced).ObjectiveOf("first-slug"), Is.Not.Null, "the reader accepts it");

        Assert.That(Read(ProjectFileEditor.AddRow(spaced, "0.1", NewRow)).ObjectiveOf("new-slug")!.Number, Is.EqualTo("0.1"));
    }

    [Test]
    public void AnObjectiveAddedToTheLastPhase_OfAFileWithNoFinalLineEnding_Reads()
    {
        var noEnding = File[..File.IndexOf("## Decisions", StringComparison.Ordinal)].TrimEnd('\n');

        var (edited, number) = ProjectFileEditor.AddObjective(noEnding, 1, "Last", ["Six."]);

        Assert.That((number, Read(edited).Phases[1].Objectives.Count), Is.EqualTo(("1.2", 2)));
        Assert.That(edited, Does.StartWith(noEnding + "\n"));
    }
}
