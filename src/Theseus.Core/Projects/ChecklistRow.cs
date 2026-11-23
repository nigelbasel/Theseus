namespace Theseus.Core.Projects;

/// <summary>One item of a checklist confirmation: what it is, on one line, and why it is listed, on another.</summary>
/// <param name="Key">What the press acts on: a card's key.</param>
/// <param name="Label">Its name.</param>
/// <param name="Detail">What it is or does, and its state, in a few words.</param>
/// <param name="Note">Why it is listed, or empty.</param>
public sealed record ChecklistRow(string Key, string Label, string Detail, string Note = "");
