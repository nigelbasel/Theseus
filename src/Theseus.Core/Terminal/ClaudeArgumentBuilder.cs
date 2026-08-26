using Theseus.Core.Launching;

namespace Theseus.Core.Terminal;

/// <summary>
/// Builds the argument vector handed to <c>claude.exe</c>. The ordering is load-bearing:
/// <c>--add-dir</c> is variadic, so anything placed after it is swallowed as another directory,
/// which is why the positional prompt must come first.
/// </summary>
public static class ClaudeArgumentBuilder
{
    /// <summary>Builds the argument vector for a session launch.</summary>
    public static IReadOnlyList<string> Build(LaunchSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var argv = new List<string>
        {
            spec.Mode == LaunchMode.Resume ? "--resume" : "--session-id",
            spec.SessionId,
        };

        if (!string.IsNullOrWhiteSpace(spec.SessionName))
        {
            argv.Add("-n");
            argv.Add(spec.SessionName);
        }

        if (!string.IsNullOrWhiteSpace(spec.Prompt))
        {
            argv.Add(spec.Prompt);
        }

        if (spec.AddDirectories.Count > 0)
        {
            argv.Add("--add-dir");
            argv.AddRange(spec.AddDirectories);
        }

        return argv;
    }
}
