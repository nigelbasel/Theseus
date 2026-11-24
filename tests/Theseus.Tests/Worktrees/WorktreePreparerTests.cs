using Theseus.Core.Worktrees;

namespace Theseus.Tests.Worktrees;

/// <summary>
/// Preparing a card's worktrees with real git (worktree-per-repo AC 8 to 13): two repos on one branch, off the primary or
/// the trunk; adopted when already there; refused on a wrong branch or a missing clone before anything is made; nothing
/// when the row's branch is none; a worked-in-place repo given as its clone.
/// </summary>
[TestFixture]
public class WorktreePreparerTests
{
    private const string Branch = "nigel/feature";
    private const string Primary = "nigel/project";

    private ScratchRepos _scratch = null!;

    [SetUp]
    public void SetUp() => _scratch = new ScratchRepos();

    [TearDown]
    public void TearDown() => _scratch.Dispose();

    [Test]
    public void ACardSpanningTwoRepos_GetsAWorktreeInEach_OnOneBranch_OffThePrimary()
    {
        var alpha = _scratch.Repo("alpha");
        var beta = _scratch.Repo("beta");
        _scratch.RemoteBranch(alpha, Primary);
        _scratch.RemoteBranch(beta, Primary);

        var prepared = Prepare("alpha, beta");

        Assert.That(prepared.Refusal, Is.Null);
        Assert.That(prepared.Folders, Is.EqualTo(new[] { Folder("alpha"), Folder("beta") }));
        Assert.That(prepared.Repos.Select(repo => repo.Kind), Is.All.EqualTo(WorktreeKind.Made));
        Assert.That(prepared.Repos.Select(repo => Theseus.Core.Domain.GitBranchReader.Read(repo.Folder)), Is.All.EqualTo(Branch));
        Assert.That(MergeBase(Folder("alpha"), $"origin/{Primary}"), Is.True);
        Assert.That(_scratch.Upstream(Folder("alpha"), Branch), Is.Null, "a new branch is made with no upstream");
        Assert.That(prepared.Opening, Does.Contain(Folder("beta")));
    }

    [Test]
    public void AProjectWhosePrimaryIsInNoCodeRepo_BranchesOffTheTrunk()
    {
        _scratch.Repo("alpha");

        var prepared = Prepare("alpha");

        Assert.That(prepared.Refusal, Is.Null);
        Assert.That(MergeBase(Folder("alpha"), "origin/main"), Is.True);
    }

    [Test]
    public void ABranchOnlyOnTheOrigin_IsCheckedOutTrackingIt()
    {
        var alpha = _scratch.Repo("alpha");
        _scratch.RemoteBranch(alpha, Branch);

        Assert.That(Prepare("alpha").Refusal, Is.Null);
        Assert.That(_scratch.Upstream(Folder("alpha"), Branch), Is.EqualTo($"origin/{Branch}"));
    }

    [Test]
    public void AWorktreeAlreadyThereOnTheBranch_IsAdopted()
    {
        _scratch.Repo("alpha");
        Prepare("alpha");

        var again = Prepare("alpha");

        Assert.That(again.Refusal, Is.Null);
        Assert.That(again.Repos.Single().Kind, Is.EqualTo(WorktreeKind.Adopted));
        Assert.That(again.Notice, Does.Contain("1 already there"));
    }

    [Test]
    public void AWorktreeThereOnAnotherBranch_StopsTheStart_SayingSo()
    {
        var alpha = _scratch.Repo("alpha");
        _scratch.Must(alpha, "worktree", "add", "-b", "other", Folder("alpha"));

        var prepared = Prepare("alpha");

        Assert.That(prepared.MayStart, Is.False);
        Assert.That(prepared.Refusal, Does.Contain("is on other, not nigel/feature"));
    }

    [Test]
    public void AMissingClone_StopsTheStart_BeforeAnyWorktreeIsMade()
    {
        _scratch.Repo("alpha");

        var prepared = Prepare("alpha, missing");

        Assert.That(prepared.MayStart, Is.False);
        Assert.That(prepared.Refusal, Does.Contain("No clone of missing was found in"));
        Assert.That(prepared.Refusal, Does.Contain("Theseus does not clone"));
        Assert.That(Directory.Exists(Folder("alpha")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_scratch.Clones, "github", "owner", "missing")), Is.False);
    }

    [Test]
    public void ABranchCheckedOutElsewhere_IsAdoptedWhereItIs_AndNothingIsMade()
    {
        var alpha = _scratch.Repo("alpha");
        var elsewhere = Path.GetFullPath(Path.Combine(_scratch.Root, "elsewhere"));
        _scratch.Must(alpha, "worktree", "add", "-b", Branch, elsewhere);

        var prepared = Prepare("alpha");

        Assert.That(prepared.Refusal, Is.Null);
        Assert.That(prepared.Repos.Single(), Is.EqualTo(new PreparedRepo("alpha", alpha, elsewhere, Branch, WorktreeKind.Adopted)));
        Assert.That(Directory.Exists(Folder("alpha")), Is.False);
    }

    [Test]
    public void TheBranchCheckedOutInTheCloneItself_StopsTheStart()
    {
        var alpha = _scratch.Repo("alpha");
        _scratch.Must(alpha, "switch", "-c", Branch);

        Assert.That(Prepare("alpha").Refusal, Does.Contain("is checked out in the clone"));
    }

    [Test]
    public void AFolderInTheWayThatIsNoWorktree_StopsTheStart()
    {
        _scratch.Repo("alpha");
        Directory.CreateDirectory(Folder("alpha"));
        File.WriteAllText(Path.Combine(Folder("alpha"), "notes.txt"), "mine");

        Assert.That(Prepare("alpha").Refusal, Does.Contain("is not empty and is not a worktree"));
    }

    [TestCase("none")]
    [TestCase("main")]
    public void ARowOnNoneOrATrunk_MakesNothing(string branch)
    {
        _scratch.Repo("alpha");

        var prepared = Prepare("alpha", branch);

        Assert.That(prepared, Is.SameAs(WorktreePreparation.Nothing));
        Assert.That(Directory.Exists(_scratch.Worktrees), Is.False);
    }

    [Test]
    public void NoClonesFolderChosen_MakesNothing()
    {
        _scratch.Repo("alpha");

        var prepared = new WorktreePreparer(_scratch.Git).Prepare(
            new WorktreeRequest("feature", Branch, "alpha", null, null, Primary, null, _scratch.Worktrees));

        Assert.That(prepared, Is.SameAs(WorktreePreparation.Nothing));
    }

    [Test]
    public void AWorkedInPlaceRepo_IsGivenAsItsClone_WithNoWorktree_AndItsBranchSaid()
    {
        var alpha = _scratch.Repo("alpha");
        const string project = """
            ## Repos

            | Repo | Role | Autonomy |
            |---|---|---|
            | github/owner/alpha | code (worked in place, not in a worktree) | L0 |
            """;

        var prepared = new WorktreePreparer(_scratch.Git).Prepare(
            new WorktreeRequest("feature", Branch, "alpha", project, null, Primary, _scratch.Clones, _scratch.Worktrees));

        Assert.That(prepared.Refusal, Is.Null);
        Assert.That(prepared.Repos.Single().Kind, Is.EqualTo(WorktreeKind.InPlace));
        Assert.That(prepared.Folders, Is.EqualTo(new[] { alpha }));
        Assert.That(prepared.Notice, Does.Contain("alpha worked in place on main, not nigel/feature"));
        Assert.That(Directory.Exists(_scratch.Worktrees), Is.False);
    }

    private WorktreePreparation Prepare(string repos, string branch = Branch) =>
        new WorktreePreparer(_scratch.Git).Prepare(
            new WorktreeRequest("feature", branch, repos, null, null, Primary, _scratch.Clones, _scratch.Worktrees));

    private string Folder(string repo) => Path.GetFullPath(Path.Combine(_scratch.Worktrees, "feature", repo));

    private bool MergeBase(string folder, string ancestor) =>
        _scratch.Git.Run(folder, "merge-base", "--is-ancestor", ancestor, "HEAD").Succeeded
        && _scratch.Git.Run(folder, "rev-parse", ancestor).Output == _scratch.Git.Run(folder, "rev-parse", "HEAD").Output;
}
