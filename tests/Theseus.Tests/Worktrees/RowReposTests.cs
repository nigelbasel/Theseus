using Theseus.Core.Worktrees;

namespace Theseus.Tests.Worktrees;

/// <summary>
/// Which code repos a card's row names (worktree-per-repo AC 1 to 4): the Repos cell split and cleaned, the project's home
/// left out, short names widened from the Repos table, and worked-in-place repos marked.
/// </summary>
[TestFixture]
public class RowReposTests
{
    private const string Project = """
        # A project

        **Primary branch:** <user>/<project>

        ## Repos

        | Repo | Role | Autonomy |
        |---|---|---|
        | local/notes (`<home folder>`) | home: project file and records | L0 |
        | github.com/owner/app | code | L0 |
        | host/owner/core, host/owner/core-extras | read; changed only when a bug lies there (worked in place, not in a worktree) | L0 |

        ## Phase 0
        """;

    [Test]
    public void TheCell_IsSplitOnCommas_WithBackticksAndParenthesesDropped()
    {
        var repos = RowRepos.For("`host/owner/one`, host/owner/two (changed in phase 2)", null);

        Assert.That(repos.Select(repo => repo.Entry), Is.EqualTo(new[] { "host/owner/one", "host/owner/two" }));
    }

    [TestCase("read-only")]
    [TestCase("none (reads only)")]
    [TestCase("the holdout repository")]
    [TestCase("[to be recorded]")]
    [TestCase("")]
    public void ProseNoneAndReadOnly_AreNotRepos(string cell)
    {
        Assert.That(RowRepos.For(cell, Project), Is.Empty);
    }

    [Test]
    public void TheHome_IsLeftOut_ByItsRole()
    {
        Assert.That(RowRepos.For("local/notes, app", Project).Select(repo => repo.Entry), Is.EqualTo(new[] { "app" }));
    }

    [Test]
    public void TheHome_IsLeftOut_WhenTheRowNamesTheProjectsOwnFolder()
    {
        Assert.That(RowRepos.For("bug-fixing", Project, "bug-fixing"), Is.Empty);
    }

    [Test]
    public void AShortEntry_IsWidened_ToItsReposTableReference()
    {
        var repo = RowRepos.For("app", Project).Single();

        Assert.That(repo.Reference, Is.EqualTo("github.com/owner/app"));
        Assert.That(repo.IsWorkedInPlace, Is.False);
    }

    [Test]
    public void WorkedInPlace_AppliesToEveryReferenceInItsReposCell()
    {
        var repos = RowRepos.For("host/owner/core, core-extras", Project);

        Assert.That(repos.Select(repo => repo.IsWorkedInPlace), Is.EqualTo(new[] { true, true }));
    }

    [Test]
    public void AnEntryInNoReposRow_IsKeptAsWritten()
    {
        var repo = RowRepos.For("host/owner/elsewhere", Project).Single();

        Assert.That(repo.Reference, Is.EqualTo("host/owner/elsewhere"));
    }

    [TestCase(null, false)]
    [TestCase("", false)]
    [TestCase("none", false)]
    [TestCase("main", false)]
    [TestCase("master", false)]
    [TestCase("[not recorded]", false)]
    [TestCase("nigel/feature", true)]
    public void OnlyARealBranch_MakesWorktrees(string? branch, bool makes)
    {
        Assert.That(RowRepos.MakesWorktrees(branch), Is.EqualTo(makes));
    }
}
