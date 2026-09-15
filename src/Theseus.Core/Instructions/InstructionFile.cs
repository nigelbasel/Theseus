namespace Theseus.Core.Instructions;

/// <summary>One instruction file a session would read, and when.</summary>
/// <param name="Path">The file.</param>
/// <param name="Scope">Where it sits in the reading order.</param>
/// <param name="Load">Whether it is read at start, on demand, or not at all.</param>
/// <param name="ImportDepth">How many imports away from a file that loads in its own right; zero for that file.</param>
/// <param name="When">When it is read, said the way a person would.</param>
public sealed record InstructionFile(
    string Path,
    InstructionScope Scope,
    InstructionLoad Load,
    int ImportDepth,
    string When);
