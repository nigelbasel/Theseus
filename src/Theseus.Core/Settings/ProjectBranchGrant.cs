namespace Theseus.Core.Settings;

/// <summary>
/// The owner's grant of branch rules to a project (1.1e AC 12b), pinned to the primary branch the project's file named when
/// he ticked the box (review of PR 2c, finding 2): sessions write project.md, so a primary branch changed since grants
/// nothing until he ticks again.
/// </summary>
/// <param name="Since">When he granted it.</param>
/// <param name="Primary">The primary branch it covers.</param>
public sealed record ProjectBranchGrant(DateTimeOffset Since, string Primary)
{
    /// <summary>Which rules he granted; the first kind for a grant kept before there were two (2.4 W2).</summary>
    public BranchGrantKind Kind { get; init; } = BranchGrantKind.MergeIntoPrimary;
}
