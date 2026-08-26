namespace Theseus.Core.Domain;

/// <summary>A pull request a workstream mentions, and where to open it.</summary>
/// <param name="Number">The pull request number.</param>
/// <param name="Url">Its address, or null when the host could not be addressed.</param>
public sealed record PullRequestLink(int Number, string? Url);
