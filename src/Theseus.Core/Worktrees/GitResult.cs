namespace Theseus.Core.Worktrees;

/// <summary>What one git call gave back.</summary>
/// <param name="ExitCode">Its exit code; -1 when it could not start or was stopped at its cap.</param>
/// <param name="Output">What it wrote to standard output.</param>
/// <param name="Error">What it wrote to standard error, or why it did not run.</param>
public sealed record GitResult(int ExitCode, string Output, string Error)
{
    /// <summary>Whether it succeeded.</summary>
    public bool Succeeded => ExitCode == 0;

    /// <summary>The first line of its error, for a notice: git's own words, not a paraphrase.</summary>
    public string FirstErrorLine =>
        (Error.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
         ?? Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault()
         ?? $"git exited with {ExitCode}");
}
