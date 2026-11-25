using Theseus.App.ViewModels;
using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// The project's title edited in Project properties (1.1e AC 12a): written to the file's <c># </c> line, refused with the
/// reason in the dialog when the file changed underneath or the title is empty; the file's path read only, saying why.
/// </summary>
[TestFixture]
public class ProjectTitleTests
{
    private const string File =
        "# Theseus bootstrap\r\n\r\n**Kit:** method-v0.2\r\n**Primary branch:** owner/project\r\n\r\n" +
        "## Phase 0: Start\r\n\r\n**Exit gate:** the owner says so.\r\n\r\n" +
        "### Objective 0.1: First\r\n\r\n- [ ] One.\r\n\r\n**Pipeline:** feature\r\n" +
        "| Workstream | Slug | Branch | Repos |\r\n|---|---|---|---|\r\n\r\n" +
        "## Decisions\r\n\r\n| Date | Decision | By |\r\n|---|---|---|\r\n";

    [Test]
    public void SetTitle_ChangesOnlyTheFirstLine_KeepingTheFilesLineEndings()
    {
        var edited = ProjectFileEditor.SetTitle(File, "Theseus bootstrap", "  Theseus ");

        Assert.That(edited, Is.EqualTo("# Theseus" + File["# Theseus bootstrap".Length..]));
        Assert.That(ProjectFileReader.Read("theseus", edited).File!.Title, Is.EqualTo("Theseus"));
    }

    [Test]
    public void SetTitle_IsRefused_WhenTheFileChangedUnderneath_OrTheTitleIsEmpty()
    {
        Assert.That(() => ProjectFileEditor.SetTitle(File, "Something else", "Theseus"),
            Throws.TypeOf<ProjectEditRefusedException>().With.Message.Contains("it changed since this was opened"));
        Assert.That(() => ProjectFileEditor.SetTitle(File, "Theseus bootstrap", "   "),
            Throws.TypeOf<ProjectEditRefusedException>().With.Message.EqualTo("a project needs a title"));
    }

    [Test]
    public void TheDialog_WritesAChangedTitleAtOk_AndKeepsARefusalShown()
    {
        var written = new List<string>();
        string? refusal = "theseus: project.md's title is now \"X\", not \"Theseus bootstrap\": it changed since this was opened";
        var properties = new ProjectPropertiesViewModel("theseus", "Theseus bootstrap", "#2F7D32", @"<root>\theseus\project.md")
        {
            WriteTitle = title =>
            {
                written.Add(title);

                return refusal;
            },
        };

        Assert.That(properties.Apply(), Is.True, "an unchanged title writes nothing");
        Assert.That(written, Is.Empty);

        properties.Title = "Theseus";

        Assert.That(properties.Apply(), Is.False);
        Assert.That((properties.HasError, properties.Error), Is.EqualTo((true, refusal)));

        refusal = null;

        Assert.That(properties.Apply(), Is.True);
        Assert.That(written, Is.EqualTo(new[] { "Theseus", "Theseus" }));
    }

    [Test]
    public void TheDialog_RefusesAnEmptyTitle_WithoutWriting_AndTyping_ClearsTheRefusal()
    {
        var written = 0;
        var properties = new ProjectPropertiesViewModel("theseus", "Theseus bootstrap", "#2F7D32", @"<root>\theseus\project.md")
        {
            WriteTitle = _ =>
            {
                written++;

                return null;
            },
        };

        properties.Title = " ";

        Assert.That((properties.Apply(), properties.Error, written), Is.EqualTo((false, "A project needs a title.", 0)));

        properties.Title = "Theseus";

        Assert.That(properties.HasError, Is.False);
        Assert.That(properties.FileToolTip, Does.Contain("Read only"));
    }
}
