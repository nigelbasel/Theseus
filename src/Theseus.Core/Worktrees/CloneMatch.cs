namespace Theseus.Core.Worktrees;

/// <summary>Where the layout rule found a repo's clone: one clone, or the candidates when there is not exactly one.</summary>
/// <param name="ClonePath">The clone, or null.</param>
/// <param name="Candidates">Every clone that matched; empty when none did.</param>
public sealed record CloneMatch(string? ClonePath, IReadOnlyList<string> Candidates);
