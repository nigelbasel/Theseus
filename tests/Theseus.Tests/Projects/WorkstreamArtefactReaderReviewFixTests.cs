using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// The artefact reader after the review of PR 1: the move to method-v0.2 measured on the primary's first-parent line
/// (item 3), and a branch named in a table never reaching git as an option (item 8).
/// </summary>
[TestFixture]
public class WorkstreamArtefactReaderReviewFixTests
{
    private const string Primary = "owner/project";

    private static readonly ProjectFile Project = new("a-project", "A project", MethodKits.Current, Primary, []);

    private TestRepository _repository = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = TestRepository.Create();
        _repository.Branch(Primary);
    }

    [TearDown]
    public void TearDown() => _repository.Dispose();

    [Test]
    public void WorkMergedWhileTheKitMoveWasOnItsOwnBranch_IsBeforeTheKit()
    {
        _repository.Branch("owner/kit");
        _repository.Commit("projects/a-project.md", $"# A project\n\n{MethodKits.CurrentKitLine}\n");
        _repository.Checkout(Primary);
        _repository.Commit("work/old-slug/spec.md", "# Written on the primary while the move was still a branch\n");
        var merge = _repository.Merge("owner/kit");
        var repository = new GitRepositoryReader(new GitCli(), _repository.Folder);

        var parent = WorkstreamArtefactReader.KitMoveParent(repository, Project);
        var artefacts = WorkstreamArtefactReader.Read(repository, Project, new ProjectWorkstreamRow("Old", "old-slug", Primary, "Repo", 1), parent);

        Assert.That(parent, Is.EqualTo(repository.Parent(merge)), "the move is the merge, and before it is the primary as it stood");
        Assert.That(artefacts.IsBeforeKit, Is.True);
    }

    [Test]
    public void ABranchCellThatLooksLikeAnOption_NeverReachesGitAsOne()
    {
        var git = new RecordingGit(new GitCli());
        var repository = new GitRepositoryReader(git, _repository.Folder);

        var artefacts = WorkstreamArtefactReader.Read(repository, Project,
            new ProjectWorkstreamRow("Odd", "odd-slug", "--output=somewhere", "Repo", 1), null);

        Assert.That(artefacts.Source.IsOnMachine, Is.False);
        Assert.That(git.Commands.SelectMany(command => command), Has.None.StartsWith("--output"));
    }
}
