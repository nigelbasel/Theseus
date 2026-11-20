namespace Theseus.Core.Projects;

/// <summary>What one git command gave back.</summary>
/// <param name="Ran">Whether git could be started and answered in time.</param>
/// <param name="ExitCode">Its exit code, when it ran.</param>
/// <param name="Output">Its standard output.</param>
/// <param name="Error">Its standard error, or why it could not be run.</param>
public sealed record GitOutput(bool Ran, int ExitCode, string Output, string Error)
{
    /// <summary>Whether it ran and exited with 0.</summary>
    public bool Succeeded => Ran && ExitCode == 0;

    /// <summary>A command that could not be run at all.</summary>
    public static GitOutput NotRun(string reason) => new(false, -1, string.Empty, reason);
}
