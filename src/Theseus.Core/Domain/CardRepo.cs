namespace Theseus.Core.Domain;

/// <summary>One repository a card covers.</summary>
/// <param name="Path">Its root directory.</param>
/// <param name="Name">The root's folder name, which is how the board names it.</param>
/// <param name="Branch">The branch it has checked out ("detached at ..." when detached), or null if unreadable.</param>
public sealed record CardRepo(string Path, string Name, string? Branch);
