using Theseus.Core.Launching;

namespace Theseus.Launcher;

/// <summary>The message left in a tab whose session failed, so the reason survives the process.</summary>
public static class ExitBanner
{
    /// <summary>Builds the banner lines for a finished session.</summary>
    public static IReadOnlyList<string> Build(ClaudeRunResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (!result.HoldTab)
        {
            return [];
        }

        return
        [
            string.Empty,
            $"Claude Code exited with code {result.ExitCode}.",
            $"Session {result.SessionId}.",
            $"This tab is held open for {LauncherHold.Limit.TotalMinutes:F0} minutes so this can be read. Press Enter to close it now.",
        ];
    }
}
