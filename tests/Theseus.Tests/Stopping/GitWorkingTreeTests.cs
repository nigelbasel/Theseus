using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>Theseus creates no handover file inside a git working tree (handoff-names review S5).</summary>
[TestFixture]
public class GitWorkingTreeTests
{
    [Test]
    public void AFolderBelowAGitFolder_IsInside()
    {
        var root = Directory.CreateTempSubdirectory("git-tree-").FullName;

        try
        {
            Directory.CreateDirectory(Path.Combine(root, ".git"));
            var notes = Directory.CreateDirectory(Path.Combine(root, "docs", "notes")).FullName;

            Assert.That(GitWorkingTree.Contains(notes), Is.True);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void AWorktreesGitFile_CountsToo()
    {
        var root = Directory.CreateTempSubdirectory("git-tree-").FullName;

        try
        {
            File.WriteAllText(Path.Combine(root, ".git"), "gitdir: elsewhere");

            Assert.That(GitWorkingTree.Contains(root), Is.True);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void AFolderWithNoGitAbove_IsNotInside()
    {
        var root = Directory.CreateTempSubdirectory("git-tree-").FullName;

        try
        {
            Assert.That(GitWorkingTree.Contains(root), Is.False, "the temp folder is not inside a repository");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
