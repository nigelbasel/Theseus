using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// Clear up verifiers… takes a cleared card's rows out of project.md, which the view drew as "Spec · not started"
/// (git-22, 2026-11-23: 1.3's verify-…-search-and-tags and its -2), and Undo puts them back where they were.
/// </summary>
[TestFixture]
public class ClearedRowsTests
{
    private const string File =
        "# A project\n\n**Kit:** method-v0.2\n**Primary branch:** owner/project\n\n" +
        "## Phase 0: Start\n\n**Exit gate:** the owner says so.\n\n" +
        "### Objective 0.1: First\n\n- [ ] One.\n\n**Pipeline:** feature\n" +
        "| Workstream | Slug | Branch | Repos | What |\n|---|---|---|---|---|\n" +
        "| git-22 | first-slug | owner/first-slug | repo | The build |\n" +
        "| verify-first | verify-first-slug | none | read-only |  |\n" +
        "| verify-first-2 | verify-first-slug-2 | none | read-only |  |\n" +
        "| git-23 | later-slug | owner/later-slug | repo | Later |\n\n" +
        "## Decisions\n\n| Date | Decision | By |\n|---|---|---|\n";

    private static readonly string[] Cleared = ["verify-first", "verify-first-2"];

    private static string Without(string text, params string[] slugs) =>
        string.Join('\n', text.Split('\n').Where(line => !slugs.Any(slug => line.Contains($"| {slug} |", StringComparison.Ordinal))));

    [Test]
    public void ClearingUp_TakesOutTheCardsRows_AndNothingElse()
    {
        var (text, rows) = ProjectFileEditor.TakeRows(File, Cleared);

        Assert.That(text, Is.EqualTo(Without(File, "verify-first-slug", "verify-first-slug-2")));
        Assert.That(rows.Select(row => row.Slug), Is.EqualTo(new[] { "verify-first-slug", "verify-first-slug-2" }));
        Assert.That(rows[0].Objective, Is.EqualTo("0.1"));
    }

    [Test]
    public void Undo_PutsTheRowsBackExactlyWhereTheyWere()
    {
        var (text, rows) = ProjectFileEditor.TakeRows(File, Cleared);

        Assert.That(ProjectFileEditor.PutBackRows(text, rows), Is.EqualTo(File));
    }

    [Test]
    public void Undo_InAFileWithWindowsLineEndings_KeepsThem()
    {
        var windows = File.Replace("\n", "\r\n", StringComparison.Ordinal);
        var (text, rows) = ProjectFileEditor.TakeRows(windows, Cleared);

        Assert.That(ProjectFileEditor.PutBackRows(text, rows), Is.EqualTo(windows));
    }

    [Test]
    public void Undo_OfARowAlreadyBack_ChangesNothing()
    {
        var (_, rows) = ProjectFileEditor.TakeRows(File, Cleared);

        Assert.That(ProjectFileEditor.PutBackRows(File, rows), Is.EqualTo(File), "a second Undo, or a session that wrote it again");
    }

    [Test]
    public void Undo_WhenTheRowAboveHasGone_PutsItAtTheEndOfItsTable()
    {
        var (_, rows) = ProjectFileEditor.TakeRows(File, ["verify-first-2"]);
        var edited = Without(File, "verify-first-slug", "verify-first-slug-2");

        var back = ProjectFileEditor.PutBackRows(edited, rows);

        Assert.That(back, Does.Contain("| git-23 | later-slug | owner/later-slug | repo | Later |\n| verify-first-2 | verify-first-slug-2 |"));
    }

    [Test]
    public void ACardWithNoRow_LeavesTheFileAsItWas()
    {
        var (text, rows) = ProjectFileEditor.TakeRows(File, ["not-in-the-file"]);

        Assert.That(text, Is.EqualTo(File));
        Assert.That(rows, Is.Empty);
    }

    [Test]
    public void TheTakenRows_AreKeptWithTheClearUp_ForUndoAfterARestart()
    {
        var (_, rows) = ProjectFileEditor.TakeRows(File, Cleared);
        var clearUp = VerifierClearUp.From(new VerifierClearUpRecord("a-project", Cleared, Cleared, []));

        clearUp.TookRows(rows);

        Assert.That(VerifierClearUp.From(clearUp.Record).Record.Rows, Is.EqualTo(rows));
    }

    [Test]
    public void TheTakenRows_SurviveTheSettingsFile_AndAnOlderRecordHasNone()
    {
        var folder = Directory.CreateTempSubdirectory("theseus-cleared-rows-").FullName;

        try
        {
            var store = new Theseus.Core.Settings.SettingsStore(Path.Combine(folder, "settings.json"));
            var (_, rows) = ProjectFileEditor.TakeRows(File, Cleared);
            store.Write(store.Read() with { VerifierClearUp = new VerifierClearUpRecord("a-project", Cleared, Cleared, []) { Rows = rows } });

            Assert.That(store.Read().VerifierClearUp!.Rows, Is.EqualTo(rows));

            store.Write(store.Read() with { VerifierClearUp = new VerifierClearUpRecord("a-project", Cleared, Cleared, []) });
            var path = Path.Combine(folder, "settings.json");
            var json = System.Text.Json.Nodes.JsonNode.Parse(System.IO.File.ReadAllText(path))!.AsObject();
            var record = json.First(each => string.Equals(each.Key, "verifierClearUp", StringComparison.OrdinalIgnoreCase)).Value!.AsObject();
            Assert.That(record.Remove(record.First(each => string.Equals(each.Key, "rows", StringComparison.OrdinalIgnoreCase)).Key), Is.True);
            System.IO.File.WriteAllText(path, json.ToJsonString());

            Assert.That(VerifierClearUp.From(store.Read().VerifierClearUp!).Rows, Is.Empty, "a record kept before clear-ups took rows");
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
