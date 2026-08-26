namespace Theseus.Core.Launching;

/// <summary>
/// Detects the one environment that quietly ruins every session Theseus starts.
///
/// A Claude Code session started from inside another one inherits a marker that turns transcript
/// persistence off. Such a session runs and looks entirely normal, but writes no transcript, never
/// registers, and cannot be resumed — so the work is only discovered to be unrecoverable the next
/// time someone tries to pick it up. Theseus does not alter the environment it was handed; it says
/// so instead, while there is still a chance to close it and start again from the desktop.
/// </summary>
public static class ChildSessionCheck
{
    private static readonly string[] Markers = ["CLAUDE_CODE_CHILD_SESSION", "CLAUDECODE"];

    /// <summary>What is wrong with the environment, or null when nothing is.</summary>
    public static string? Warning(Func<string, string?> readVariable)
    {
        ArgumentNullException.ThrowIfNull(readVariable);

        var present = Markers
            .Where(marker => !string.IsNullOrEmpty(readVariable(marker)))
            .ToList();

        return present.Count == 0
            ? null
            : "Theseus was started from inside a Claude Code session. Sessions it launches will not "
              + "save a transcript and cannot be resumed. Close Theseus and start it from the "
              + "Start Menu instead.";
    }

    /// <summary>What is wrong with this process's environment, or null when nothing is.</summary>
    public static string? Warning() => Warning(Environment.GetEnvironmentVariable);
}
