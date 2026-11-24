namespace Theseus.Core.CiStatus;

/// <summary>What one read of a repository found. Only a <see cref="RepoReadingKind.Read"/> carries lists.</summary>
/// <param name="Kind">What the read came to.</param>
/// <param name="At">When it was made.</param>
/// <param name="PullRequests">The open pull requests.</param>
/// <param name="Runs">The newest workflow runs.</param>
public sealed record RepoReading(RepoReadingKind Kind, DateTimeOffset At, IReadOnlyList<OpenPullRequest> PullRequests,
    IReadOnlyList<CiRun> Runs)
{
    /// <summary>When the limit resets, for a <see cref="RepoReadingKind.Limited"/> read.</summary>
    public DateTimeOffset? LimitResetsAt { get; init; }

    /// <summary>A read that found nothing it can show, of the given kind.</summary>
    public static RepoReading Unknown(RepoReadingKind kind, DateTimeOffset at) => new(kind, at, [], []);
}
