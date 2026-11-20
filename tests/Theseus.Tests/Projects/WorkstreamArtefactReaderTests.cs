using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// A slug's records are read from its <c>work\&lt;slug&gt;\</c> folder in the project home, as plain files (1.1b AC 2
/// and 4): its file names, the texts the stages and gates need, and the spec's identity by content (AC 5).
/// </summary>
[TestFixture]
public class WorkstreamArtefactReaderTests
{
    private TestProjectsFolder _projects = null!;

    [SetUp]
    public void SetUp() => _projects = TestProjectsFolder.Create();

    [TearDown]
    public void TearDown() => _projects.Dispose();

    private ProjectFile Project(string header = "")
    {
        var text = TestProjectsFolder.ProjectText("A project", body: TestProjectsFolder.OneObjective("a-slug", "old-slug"), header: header);
        _projects.Write("a-project/project.md", text);

        return ProjectFileReader.Read("a-project", text).File ?? throw new AssertionException("the test's project file did not read");
    }

    private WorkstreamArtefacts Read(string slug = "a-slug", ProjectFile? project = null) =>
        WorkstreamArtefactReader.Read(_projects.Home("a-project"), project ?? Project(), slug);

    [Test]
    public void ASlugWithNoFolder_IsNotStarted()
    {
        var artefacts = Read();

        Assert.That((artefacts.Source.Exists, artefacts.Source.Describe, artefacts.Files.Count), Is.EqualTo((false, "no work folder yet", 0)));
    }

    [Test]
    public void ASlugsFolder_IsReadWithItsFilesAndTexts()
    {
        _projects.Write("a-project/work/a-slug/spec.md", "# Spec\n");
        _projects.Write("a-project/work/a-slug/evidence.md", "# Evidence\n");

        var artefacts = Read();

        Assert.That(artefacts.Files, Is.EquivalentTo(new[] { "spec.md", "evidence.md" }));
        Assert.That((artefacts.SpecText, artefacts.EvidenceText), Is.EqualTo(("# Spec\n", "# Evidence\n")));
        Assert.That(artefacts.Source.Folder, Is.EqualTo(Path.Combine(_projects.Home("a-project"), "work", "a-slug")));
    }

    [Test]
    public void ASpecsIdentity_IsItsContent_LineEndingsAside()
    {
        _projects.Write("a-project/work/a-slug/spec.md", "# Spec\r\n1. One.\r\n");

        Assert.That(Read().SpecHash, Is.EqualTo(SpecIdentity.Hash("# Spec\n1. One.\n")));
    }

    [Test]
    public void ABugsRuleIdentity_IsItsSpecSectionAlone_SoWritingTheFixDoesNotChangeIt()
    {
        _projects.Write("a-project/work/a-slug/fix.md", "# Fix\n\n## Spec\n1. The rule.\n");
        var before = Read().RuleHash;

        _projects.Write("a-project/work/a-slug/fix.md", "# Fix\n\n## Spec\n1. The rule.\n\n## Cause\nWhy.\n");

        Assert.That(Read().RuleHash, Is.EqualTo(before).And.Not.Null);
    }

    [Test]
    public void ASlugTheProjectFileNamesAsBeforeTheKit_IsBeforeTheKit_AndOthersAreNot()
    {
        var project = Project(header: "**Before method-v0.2:** old-slug, `another`\n");
        _projects.Write("a-project/work/old-slug/spec.md", "# Spec\n");
        _projects.Write("a-project/work/a-slug/spec.md", "# Spec\n");

        Assert.That(project.BeforeKit, Is.EquivalentTo(new[] { "old-slug", "another" }));
        Assert.That(Read("old-slug", project).IsBeforeKit, Is.True);
        Assert.That(Read("a-slug", project).IsBeforeKit, Is.False);
    }
}
