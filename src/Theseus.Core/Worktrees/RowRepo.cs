namespace Theseus.Core.Worktrees;

/// <summary>One code repo a card's row names.</summary>
/// <param name="Entry">The entry as the row writes it.</param>
/// <param name="Reference">The fullest reference known for it: the Repos table's, when the entry is short.</param>
/// <param name="IsWorkedInPlace">Whether it is worked in its clone, with no worktree.</param>
public sealed record RowRepo(string Entry, string Reference, bool IsWorkedInPlace);
