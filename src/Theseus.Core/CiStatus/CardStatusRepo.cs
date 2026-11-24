namespace Theseus.Core.CiStatus;

/// <summary>A repository a card works in, as its status needs it.</summary>
/// <param name="Name">The name the board gives it: its folder's.</param>
/// <param name="Branch">The branch checked out, "detached at ..." when detached, or null when unreadable.</param>
/// <param name="Origin">Its origin remote, or null when it has none.</param>
public sealed record CardStatusRepo(string Name, string? Branch, string? Origin);
