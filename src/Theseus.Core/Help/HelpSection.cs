namespace Theseus.Core.Help;

/// <summary>A group of explanations under one heading.</summary>
/// <param name="Title">The heading.</param>
/// <param name="Preamble">What the whole section is about, before its entries.</param>
/// <param name="Entries">The explanations themselves.</param>
public sealed record HelpSection(string Title, string Preamble, IReadOnlyList<HelpEntry> Entries);
