namespace Theseus.Core.Running;

/// <summary>Tokens a run used, as Claude Code reported them.</summary>
/// <param name="Input">Uncached input tokens.</param>
/// <param name="Output">Output tokens.</param>
/// <param name="CacheCreation">Input tokens written to the prompt cache.</param>
/// <param name="CacheRead">Input tokens read from the prompt cache.</param>
public sealed record RunTokens(long Input = 0, long Output = 0, long CacheCreation = 0, long CacheRead = 0)
{
    /// <summary>No tokens.</summary>
    public static RunTokens None { get; } = new();

    /// <summary>
    /// The tokens a token limit counts. Cache reads are left out: every call of a long session reads the whole cached
    /// prompt again, so counting them would make the limit a measure of length, not of work.
    /// </summary>
    public long Counted => Input + Output + CacheCreation;

    /// <summary>The sum of two readings.</summary>
    public RunTokens Plus(RunTokens other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return new(Input + other.Input, Output + other.Output, CacheCreation + other.CacheCreation, CacheRead + other.CacheRead);
    }
}
