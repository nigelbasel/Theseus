namespace Theseus.Core.Finding;

/// <summary>A tag in use and how many cards carry it, for the Tags list and Options, Tags (items 12 and 13).</summary>
/// <param name="Tag">The tag.</param>
/// <param name="Count">How many cards and project cards carry it.</param>
public sealed record TagCount(string Tag, int Count);
