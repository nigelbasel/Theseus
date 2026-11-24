namespace Theseus.Core.Projects;

/// <summary>
/// Work another card of the project gave a card by starting or resuming it with an instruction (git-22, 2026-11-24): a
/// lead that tested the MCP gave its bugs to their workstream, and the view showed only the card turning busy.
/// </summary>
/// <param name="AtUtc">When it was given.</param>
/// <param name="FromKey">The card that gave it.</param>
/// <param name="FromTitle">That card's name on screen.</param>
/// <param name="Instruction">All of it, as given.</param>
public sealed record HandIn(DateTimeOffset AtUtc, string FromKey, string FromTitle, string Instruction)
{
    /// <summary>Its first line, trimmed.</summary>
    public string FirstLine => Instruction.Trim().Split('\n')[0].Trim();
}
