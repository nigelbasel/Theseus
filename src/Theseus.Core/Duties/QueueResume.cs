namespace Theseus.Core.Duties;

/// <summary>A card the overnight queue resumes now, and what it is told.</summary>
/// <param name="Card">The card's key.</param>
/// <param name="Title">Its title.</param>
/// <param name="Status">Its piece's status line, remembered so it is not resumed again until that changes.</param>
/// <param name="Instruction">What it is told.</param>
public sealed record QueueResume(string Card, string Title, string Status, string Instruction);
