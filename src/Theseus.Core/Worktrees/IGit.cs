namespace Theseus.Core.Worktrees;

/// <summary>
/// Runs git with an argument list, never a command line, so nothing a project file says can become a shell argument
/// (worktree-per-repo AC 11).
/// </summary>
public interface IGit
{
    /// <summary>Runs git in a folder and waits, up to its cap, for it to finish.</summary>
    GitResult Run(string workingDirectory, params string[] arguments);
}
