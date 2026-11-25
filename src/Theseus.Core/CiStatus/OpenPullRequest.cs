namespace Theseus.Core.CiStatus;

/// <summary>An open pull request, as GitHub's list of them gives it without a token.</summary>
/// <param name="Number">Its number.</param>
/// <param name="Title">Its title.</param>
/// <param name="HeadBranch">The branch it merges from.</param>
/// <param name="HeadRepository">"owner/name" of the repository that branch is in; another one is a fork.</param>
/// <param name="IsDraft">Whether it is still a draft.</param>
/// <param name="Url">Its page.</param>
public sealed record OpenPullRequest(int Number, string Title, string HeadBranch, string? HeadRepository, bool IsDraft, string Url);
