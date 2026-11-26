using System.Diagnostics;
using Theseus.Core.Diagnostics;
using Theseus.Core.Launching;

namespace Theseus.Core.Setup;

/// <summary>
/// What a tool prints for <c>--version</c> on this machine, for <see cref="ToolsCheck"/>. Claude Code is found as launches
/// find it; Node and git on the PATH, as Claude Code's hooks and Bash find them.
/// </summary>
public static class ToolVersions
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(5);

    /// <summary>The version output of <paramref name="program"/>, or null when it is not found or does not answer.</summary>
    /// <param name="program">One of <see cref="ToolsCheck.Claude"/>, <see cref="ToolsCheck.Node"/>, <see cref="ToolsCheck.Git"/>.</param>
    /// <param name="claudePath">Claude Code chosen by hand in Options, or empty.</param>
    public static string? Of(string program, string? claudePath = null)
    {
        var executable = program == ToolsCheck.Claude ? new ClaudeLocator(claudePath).Locate() : OnPath(program + ".exe");

        if (executable is null)
        {
            return null;
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(executable, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEndAsync();

            if (!process.WaitForExit(Patience))
            {
                process.Kill(entireProcessTree: true);

                return null;
            }

            return output.Wait(Patience) ? output.Result.Trim() : null;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            TheseusLog.Debug("{Program} --version could not be run: {Reason}", program, exception.Message);

            return null;
        }
    }

    private static string? OnPath(string name)
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty)
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim(), name);

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // An unusable PATH entry is skipped, as ClaudeLocator skips it.
            }
        }

        return null;
    }
}
