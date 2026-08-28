namespace Theseus.Core.Help;

/// <summary>One thing explained: a button, a step, or a way it goes wrong.</summary>
/// <param name="Term">What is being explained, as it appears on screen.</param>
/// <param name="Explanation">What it does, in the fewest words that are still true.</param>
public sealed record HelpEntry(string Term, string Explanation);
