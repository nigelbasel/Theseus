namespace Theseus.Core.CiStatus;

/// <summary>Reads a repository's open pull requests and newest runs from GitHub.</summary>
public interface IGitHubStatusClient
{
    /// <summary>Reads one repository with two calls at most.</summary>
    Task<GitHubRead> ReadAsync(GitHubRepoRef repo, CancellationToken cancellationToken);
}
