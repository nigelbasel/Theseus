using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// Where a slug's work is read (spec AC 5), and work from before method-v0.2 (AC 9), against real repositories: the
/// workstream's branch while unmerged, the primary once merged, and not on this machine when neither has it.
/// </summary>
[TestFixture]
public class WorkstreamArtefactReaderTests
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

    private static ProjectWorkstreamRow Row(string slug = "a-slug", string? branch = null) =>
        new("A workstream", slug, branch ?? $"owner/{slug}", "Repo", 1);

    private WorkstreamArtefacts Read(ProjectWorkstreamRow? row = null, string? kitMoveParent = null) =>
        WorkstreamArtefactReader.Read(new GitRepositoryReader(new GitCli(), _repository.Folder), Project, row ?? Row(), kitMoveParent);

    [Test]
    public void WhileItsBranchIsNotMerged_TheWorkIsReadFromItsBranch()
    {
        _repository.Branch("owner/a-slug");
        var commit = _repository.Commit("work/a-slug/spec.md", "# Spec\n");
        _repository.Checkout(Primary);

        var artefacts = Read();

        Assert.That(artefacts.Source, Is.EqualTo(new WorkstreamSource("owner/a-slug", commit, false)));
        Assert.That(artefacts.Files, Is.EquivalentTo(new[] { "spec.md" }));
        Assert.That(artefacts.SpecCommit, Is.EqualTo(commit));
        Assert.That(artefacts.Source.Describe, Is.EqualTo($"read from owner/a-slug at {commit[..7]}"));
    }

    [Test]
    public void OnceMerged_TheWorkIsReadFromThePrimary()
    {
        _repository.Branch("owner/a-slug");
        _repository.Commit("work/a-slug/spec.md", "# Spec\n");
        _repository.Checkout(Primary);
        var merge = _repository.Merge("owner/a-slug");
        _repository.Commit("work/a-slug/plan.md", "# Plan, on the primary\n");

        var artefacts = Read();

        Assert.That(artefacts.Source.Branch, Is.EqualTo(Primary));
        Assert.That(artefacts.Source.IsMerged, Is.True);
        Assert.That(artefacts.Files, Is.EquivalentTo(new[] { "spec.md", "plan.md" }));
        Assert.That(merge, Is.Not.EqualTo(artefacts.Source.Commit), "the primary's commit as read, not the merge's");
    }

    [Test]
    public void ABranchDeletedAfterItsMerge_IsReadFromThePrimary()
    {
        _repository.Branch("owner/a-slug");
        _repository.Commit("work/a-slug/spec.md", "# Spec\n");
        _repository.Checkout(Primary);
        _repository.Merge("owner/a-slug");
        _repository.DeleteBranch("owner/a-slug");

        Assert.That(Read().Source.Branch, Is.EqualTo(Primary));
    }

    [Test]
    public void WhenNeitherBranchHasTheWork_ItIsNotOnThisMachine()
    {
        var artefacts = Read(Row(branch: "owner/never-made"));

        Assert.That(artefacts.Source.IsOnMachine, Is.False);
        Assert.That(artefacts.Source.Describe, Is.EqualTo("not on this machine"));
    }

    [Test]
    public void WorkDoneOnThePrimaryItself_IsReadFromIt()
    {
        _repository.Commit("work/a-slug/research.md", "# Research\n");

        var artefacts = Read(Row(branch: Primary));

        Assert.That((artefacts.Source.Branch, artefacts.Files.Single()), Is.EqualTo((Primary, "research.md")));
    }

    [Test]
    public void ASpecsIdentity_SurvivesTheMerge_SoAnAcceptanceStillHolds()
    {
        _repository.Branch("owner/a-slug");
        _repository.Commit("work/a-slug/spec.md", "# Spec\n\nThe criteria.\n");
        var onBranch = Read().SpecHash;
        _repository.Checkout(Primary);
        _repository.Merge("owner/a-slug");

        Assert.That(Read().SpecHash, Is.EqualTo(onBranch).And.Not.Null);
    }

    [Test]
    public void ABugsRuleIdentity_IsItsSpecSectionAlone_SoWritingTheFixDoesNotChangeIt()
    {
        _repository.Branch("owner/a-slug");
        _repository.Commit("work/a-slug/fix.md", "# Fix\n\n## Spec (the rule)\n1. A rule.\n");
        var ruleOnly = Read().RuleHash;
        _repository.Commit("work/a-slug/fix.md", "# Fix\n\n## Spec (the rule)\n1. A rule.\n\n## Cause\nWhy.\n");
        _repository.Checkout(Primary);

        var artefacts = Read();

        Assert.That(artefacts.RuleHash, Is.EqualTo(ruleOnly).And.Not.Null);
        Assert.That(artefacts.FixText, Does.Contain("## Cause"));
    }

    [Test]
    public void WorkMergedBeforeTheMoveToV02_IsBeforeTheKit_AndWorkAfterItIsNot()
    {
        _repository.Commit("projects/a-project.md", "# A project\n\n**Kit:** method-v0.1\n");
        _repository.Commit("work/old-slug/spec.md", "# Old spec\n");
        _repository.Commit("projects/a-project.md", $"# A project\n\n{MethodKits.CurrentKitLine}\n**Primary branch:** {Primary}\n");
        _repository.Commit("work/new-slug/spec.md", "# New spec\n");
        var repository = new GitRepositoryReader(new GitCli(), _repository.Folder);
        var parent = WorkstreamArtefactReader.KitMoveParent(repository, Project);

        Assert.That(Read(Row("old-slug", Primary), parent).IsBeforeKit, Is.True);
        Assert.That(Read(Row("new-slug", Primary), parent).IsBeforeKit, Is.False);
    }

    [Test]
    public void OldWorkWrittenToAgainAfterTheMove_IsNotBeforeTheKit()
    {
        _repository.Commit("work/old-slug/spec.md", "# Old spec\n");
        _repository.Commit("projects/a-project.md", $"# A project\n\n{MethodKits.CurrentKitLine}\n");
        _repository.Commit("work/old-slug/plan.md", "# A plan written since\n");
        var parent = WorkstreamArtefactReader.KitMoveParent(new GitRepositoryReader(new GitCli(), _repository.Folder), Project);

        Assert.That(Read(Row("old-slug", Primary), parent).IsBeforeKit, Is.False);
    }

    [Test]
    public void UnmergedWork_IsNeverBeforeTheKit()
    {
        _repository.Branch("owner/a-slug");
        _repository.Commit("work/a-slug/spec.md", "# Spec\n");
        _repository.Checkout(Primary);
        _repository.Commit("projects/a-project.md", $"# A project\n\n{MethodKits.CurrentKitLine}\n");
        _repository.Commit("work/other/spec.md", "# Something after the move\n");
        var parent = WorkstreamArtefactReader.KitMoveParent(new GitRepositoryReader(new GitCli(), _repository.Folder), Project);

        Assert.That(Read(kitMoveParent: parent).IsBeforeKit, Is.False);
    }

    [Test]
    public void AProjectBornAtV02_HasNoMoveAndNothingBeforeIt()
    {
        var parentless = TestRepository.Create(Primary);

        try
        {
            parentless.Git("checkout", "--quiet", "--orphan", "born");
            parentless.Commit("projects/a-project.md", $"# A project\n\n{MethodKits.CurrentKitLine}\n");

            var repository = new GitRepositoryReader(new GitCli(), parentless.Folder);

            Assert.That(WorkstreamArtefactReader.KitMoveParent(repository, Project with { PrimaryBranch = "born" }), Is.Null);
        }
        finally
        {
            parentless.Dispose();
        }
    }
}
