namespace Theseus.Core.Finding;

/// <summary>A run of characters in a text: where a search word was found in a title, to be highlighted (item 3).</summary>
/// <param name="Start">Its first character.</param>
/// <param name="Length">How many characters it covers.</param>
public sealed record TextSpan(int Start, int Length);
