namespace Theseus.Core.Projects;

/// <summary>A row Clear up verifiers… took out of project.md, kept so Undo can put it back where it was.</summary>
/// <param name="Card">The card it named.</param>
/// <param name="Objective">The objective whose table it was in.</param>
/// <param name="Slug">Its slug.</param>
/// <param name="Text">The line as it was written.</param>
/// <param name="After">The slug of the row above it in that table, or null when it was the first.</param>
public sealed record ClearedRow(string Card, string Objective, string Slug, string Text, string? After);
