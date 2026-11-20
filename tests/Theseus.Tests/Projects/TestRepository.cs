using System.Diagnostics;

namespace Theseus.Tests.Projects;

/// <summary>
/// A real git repository in the temp folder, for testing what Theseus reads with git. Its git commands run with an
/// empty global configuration and identity passed on the command line, so nothing in the machine's git config is read
/// or written, and no hook or signing setting reaches them.
/// </summary>
internal sealed class TestRepository : IDisposable
{
    private readonly string _root;
    private readonly string _emptyConfig;

    private TestRepository(string root, string folder)
    {
        _root = root;
        Folder = folder;
        _emptyConfig = Path.Combine(root, "empty.gitconfig");
    }

    /// <summary>The repository's working folder.</summary>
    public string Folder { get; }

    /// <summary>Creates a repository whose first branch is <paramref name="branch"/>, with one empty commit.</summary>
    public static TestRepository Create(string branch = "main")
    {
        var root = Path.Combine(Path.GetTempPath(), "theseus-projects-" + Guid.NewGuid().ToString("N"));
        var repository = new TestRepository(root, Path.Combine(root, "repo"));

        Directory.CreateDirectory(repository.Folder);
        File.WriteAllText(repository._emptyConfig, string.Empty);
        repository.RunIn(root, "init", "--quiet", $"--initial-branch={branch}", repository.Folder);
        repository.Git("commit", "--quiet", "--allow-empty", "-m", "start");

        return repository;
    }

    /// <summary>Clones this repository beside it, so the clone has an origin whose HEAD names the default branch.</summary>
    public TestRepository CloneBeside(string name = "clone")
    {
        var clone = new TestRepository(_root, Path.Combine(_root, name));
        RunIn(_root, "clone", "--quiet", Folder, clone.Folder);

        return clone;
    }

    /// <summary>Writes a file and commits it on the branch checked out.</summary>
    public string Commit(string path, string text, string message = "a change")
    {
        var full = Path.Combine(Folder, path.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, text);
        Git("add", "--", path);
        Git("commit", "--quiet", "-m", message);

        return Head();
    }

    /// <summary>Creates a branch at the checked-out commit and checks it out.</summary>
    public void Branch(string name) => Git("checkout", "--quiet", "-b", name);

    /// <summary>Checks out a branch.</summary>
    public void Checkout(string name) => Git("checkout", "--quiet", name);

    /// <summary>Merges a branch into the checked-out one with a merge commit.</summary>
    public string Merge(string branch)
    {
        Git("merge", "--quiet", "--no-ff", "-m", $"Merge {branch}", branch);

        return Head();
    }

    /// <summary>Deletes a local branch, merged or not.</summary>
    public void DeleteBranch(string name) => Git("branch", "--quiet", "-D", name);

    /// <summary>The commit checked out.</summary>
    public string Head() => Git("rev-parse", "HEAD").Trim();

    /// <summary>Runs git in the repository and returns its output; fails the test when git does.</summary>
    public string Git(params string[] arguments) => RunIn(Folder, arguments);

    public void Dispose()
    {
        if (!Directory.Exists(_root))
        {
            return;
        }

        // Git writes its objects read-only, which Directory.Delete refuses on Windows.
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }

        Directory.Delete(_root, recursive: true);
    }

    private string RunIn(string workingDirectory, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        foreach (var argument in new[]
                 {
                     "-c", "user.name=Test", "-c", "user.email=test@example.invalid", "-c", "commit.gpgsign=false",
                     "-c", "core.autocrlf=false",
                 }.Concat(arguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["GIT_CONFIG_GLOBAL"] = _emptyConfig;
        startInfo.Environment["GIT_CONFIG_NOSYSTEM"] = "1";

        using var process = Process.Start(startInfo)!;
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed: {error.Result}");
        }

        return output.Result;
    }
}
