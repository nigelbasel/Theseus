using Theseus.Core.Worktrees;

namespace Theseus.Tests.Worktrees;

/// <summary>
/// The clear-up on archive with real git (worktree-per-repo AC 14 to 17): clean worktrees offered, dirty ones kept, none
/// another card uses, none on another branch; removing one keeps its branch, and a worktree dirtied since it was listed
/// survives.
/// </summary>
[TestFixture]
public class WorktreeClearUpTests
{
    private const string Branch = "nigel/feature";

    private ScratchRepos _scratch = null!;

    [SetUp]
    public void SetUp()
    {
        _scratch = new ScratchRepos();
        _scratch.Repo("alpha");
        _scratch.Repo("beta");

        var prepared = new WorktreePreparer(_scratch.Git).Prepare(
            new WorktreeRequest("feature", Branch, "alpha, beta", null, null, null, _scratch.Clones, _scratch.Worktrees));

        Assert.That(prepared.Refusal, Is.Null);
    }

    [TearDown]
    public void TearDown() => _scratch.Dispose();

    [Test]
    public void ACleanWorktree_IsOffered_AndADirtyOneKept()
    {
        File.WriteAllText(Path.Combine(Folder("beta"), "draft.txt"), "not committed");

        var found = Find();

        Assert.That(found.Clean.Select(each => each.Repo), Is.EqualTo(new[] { "alpha" }));
        Assert.That(found.Kept.Select(each => each.Repo), Is.EqualTo(new[] { "beta" }));
    }

    [Test]
    public void Removing_TakesTheWorktree_AndKeepsItsBranch()
    {
        foreach (var worktree in Find().Clean)
        {
            Assert.That(new WorktreeClearUp(_scratch.Git).Remove(worktree), Is.Null);
        }

        Assert.That(Directory.Exists(Folder("alpha")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_scratch.Worktrees, "feature")), Is.False, "the emptied slug folder goes too");
        Assert.That(_scratch.BranchExists(Path.Combine(_scratch.Clones, "github", "owner", "alpha"), Branch), Is.True);
    }

    [Test]
    public void AWorktreeDirtiedSinceItWasListed_SurvivesTheRemoval()
    {
        var alpha = Find().Clean.Single(each => each.Repo == "alpha");
        File.WriteAllText(Path.Combine(Folder("alpha"), "late.txt"), "written after the dialog opened");

        var refusal = new WorktreeClearUp(_scratch.Git).Remove(alpha);

        Assert.That(refusal, Does.Contain("uncommitted changes"));
        Assert.That(File.Exists(Path.Combine(Folder("alpha"), "late.txt")), Is.True);
    }

    [Test]
    public void AWorktreeAnotherCardUses_IsNotListed()
    {
        var found = new WorktreeClearUp(_scratch.Git).Find(_scratch.Worktrees, "feature", Branch,
            folder => folder.EndsWith("alpha", StringComparison.OrdinalIgnoreCase));

        Assert.That(found.Clean.Select(each => each.Repo), Is.EqualTo(new[] { "beta" }));
    }

    [Test]
    public void AWorktreeOnAnotherBranch_IsNotTheCards()
    {
        Assert.That(new WorktreeClearUp(_scratch.Git).Find(_scratch.Worktrees, "feature", "nigel/other", _ => false).IsEmpty, Is.True);
    }

    private WorktreeClearUpList Find() => new WorktreeClearUp(_scratch.Git).Find(_scratch.Worktrees, "feature", Branch, _ => false);

    private string Folder(string repo) => Path.GetFullPath(Path.Combine(_scratch.Worktrees, "feature", repo));
}
