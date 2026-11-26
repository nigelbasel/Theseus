namespace Theseus.Core.Projects;

/// <summary>What a close needs to know of one of the project's cards (archive-a-project, Nigel 2026-11-26).</summary>
/// <param name="Key">The card's key.</param>
/// <param name="Title">Its name on screen.</param>
/// <param name="HasSession">Whether a session is open on it, so the close asks it to hand over and stop.</param>
/// <param name="IsArchived">Whether it is archived already, so an archive leaves it, and Reopen leaves it archived.</param>
public sealed record ProjectCloseCard(string Key, string Title, bool HasSession, bool IsArchived);
