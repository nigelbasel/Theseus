using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// Which projects are read, and from where (spec AC 1 and 2): from the primary branch with git, found from the default
/// branch's copy of the file, whatever the folder has checked out; a folder or branch that is not there is unreadable
/// with its reason. Against real repositories, because what is being tested is what git gives back.
/// </summary>
[TestFixture]
public class ProjectCatalogTests
{
    private TestRepository _repository = null!;

    [SetUp]
    public void SetUp() => _repository = TestRepository.Create();

    [TearDown]
    public void TearDown() => _repository.Dispose();

    internal static string ProjectText(string title, string primary = "owner/project", string kit = "method-v0.2", string body = "") =>
        $"# {title}\n\n**Kit:** {kit}\n**Primary branch:** {primary}\n\n{body}";

    private IReadOnlyList<ProjectFileReading> Read(IGitReader? git = null) =>
        ProjectCatalog.Read([_repository.Folder], git ?? new GitCli());

    [Test]
    public void NoFoldersListed_RunsNoGitAndReadsNothing()
    {
        var git = new RecordingGit(new GitCli());

        Assert.That(ProjectCatalog.Read(["", "  "], git), Is.Empty);
        Assert.That(git.Commands, Is.Empty, "a board with no project repositories behaves exactly as today (AC 22)");
    }

    [Test]
    public void TheFileIsRead_FromThePrimaryBranchItsDefaultBranchCopyNames()
    {
        _repository.Commit("projects/a-project.md", ProjectText("As on main"));
        _repository.Branch("owner/project");
        _repository.Commit("projects/a-project.md", ProjectText("As on the primary"));

        var reading = Read().Single();

        Assert.That(reading.File!.Title, Is.EqualTo("As on the primary"));
        Assert.That(reading.Folder, Is.EqualTo(_repository.Folder));
    }

    [Test]
    public void TheBranchTheFolderHasCheckedOut_DoesNotMatter()
    {
        _repository.Commit("projects/a-project.md", ProjectText("As on main"));
        _repository.Branch("owner/project");
        _repository.Commit("projects/a-project.md", ProjectText("As on the primary"));
        _repository.Checkout("main");
        _repository.Branch("someone/elsewhere");
        _repository.Git("rm", "--quiet", "projects/a-project.md");
        _repository.Git("commit", "--quiet", "-m", "gone here");

        Assert.That(Read().Single().File!.Title, Is.EqualTo("As on the primary"));
    }

    [Test]
    public void InAClone_TheDefaultBranchIsTheOneOriginsHeadNames()
    {
        _repository.Commit("projects/a-project.md", ProjectText("As on main"));
        _repository.Branch("owner/project");
        _repository.Commit("projects/a-project.md", ProjectText("As on the primary"));
        _repository.Checkout("main");

        using var clone = _repository.CloneBeside();
        clone.Git("branch", "--quiet", "owner/project", "origin/owner/project");
        clone.Branch("someone/elsewhere");

        var reading = ProjectCatalog.Read([clone.Folder], new GitCli()).Single();

        Assert.That(reading.File!.Title, Is.EqualTo("As on the primary"));
    }

    [Test]
    public void AFileStillAtV01OnTheDefaultBranch_IsFoundThroughTheCheckedOutBranch()
    {
        _repository.Commit("projects/a-project.md", "# A project\n\nNot yet in the shape.\n");
        _repository.Branch("owner/project");
        _repository.Commit("projects/a-project.md", ProjectText("As on the primary"));

        Assert.That(Read().Single().File!.Title, Is.EqualTo("As on the primary"));
    }

    [Test]
    public void AFileAtV01Everywhere_IsPinned()
    {
        _repository.Commit("projects/a-project.md", "# A project\n\n**Kit:** method-v0.1\n");

        var reading = Read().Single();

        Assert.That(reading.Status, Is.EqualTo(ProjectReadingStatus.Pinned));
        Assert.That(reading.Describe, Is.EqualTo("a-project: pinned to method-v0.1"));
    }

    [Test]
    public void APrimaryBranchNotOnThisMachine_IsUnreadable()
    {
        _repository.Commit("projects/a-project.md", ProjectText("A project", primary: "owner/missing"));

        var reading = Read().Single();

        Assert.That(reading.Describe,
            Is.EqualTo("a-project: unreadable: its primary branch owner/missing is not on this machine (named on main)"));
    }

    [Test]
    public void AFileOutOfShapeOnThePrimary_IsUnreadable_NamingTheBranch()
    {
        _repository.Commit("projects/a-project.md", ProjectText("As on main"));
        _repository.Branch("owner/project");
        _repository.Commit("projects/a-project.md", ProjectText("Broken", body: "## Phase 3: out of order\n"));

        Assert.That(Read().Single().Reason,
            Is.EqualTo("line 6: phase 3 is out of order: phases are numbered from 0, one after another (on owner/project)"));
    }

    [Test]
    public void ARepositoryWithNoProjectFiles_IsUnreadable()
    {
        Assert.That(Read().Single().Describe, Is.EqualTo("repo: unreadable: no project files in projects/ on main"));
    }

    [Test]
    public void AFolderThatIsNotARepository_IsUnreadable()
    {
        var plain = Path.Combine(Path.GetTempPath(), "theseus-not-a-repo-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(plain);

        try
        {
            Assert.That(ProjectCatalog.Read([plain], new GitCli()).Single().Reason, Is.EqualTo("not a git repository"));
        }
        finally
        {
            Directory.Delete(plain);
        }
    }

    [Test]
    public void AFolderThatDoesNotExist_IsUnreadable()
    {
        var missing = Path.Combine(_repository.Folder, "no-such-folder");

        Assert.That(ProjectCatalog.Read([missing], new GitCli()).Single().Describe,
            Is.EqualTo("no-such-folder: unreadable: the folder does not exist"));
    }

    [Test]
    public void WhenGitCannotBeRun_TheFolderIsUnreadable_WithTheReason()
    {
        var reading = ProjectCatalog.Read([_repository.Folder], new GitCli("theseus-no-such-git")).Single();

        Assert.That(reading.Status, Is.EqualTo(ProjectReadingStatus.Unreadable));
        Assert.That(reading.Reason, Does.StartWith("git could not be run"));
    }

    [Test]
    public void Reading_NeverFetchesOrWrites()
    {
        _repository.Commit("projects/a-project.md", ProjectText("As on main"));
        _repository.Branch("owner/project");
        var git = new RecordingGit(new GitCli());

        Read(git);

        Assert.That(git.Commands.Select(command => command[0]).Distinct(),
            Is.SubsetOf(new[] { "rev-parse", "symbolic-ref", "ls-tree", "cat-file" }));
    }
}
