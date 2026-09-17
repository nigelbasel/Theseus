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

        // Last, because --mcp-config is variadic too. Anything after it would be read as another
        // configuration, and it ends the list of directories before it by being a flag. The session's
        // time tools are allowed alongside it, so no session stops to ask about them.
        if (!string.IsNullOrWhiteSpace(spec.McpConfig))
        {
            argv.Add("--allowedTools");
            argv.Add(Mcp.TheseusMcp.AllowedTools);
            argv.Add("--mcp-config");
            argv.Add(spec.McpConfig);
        }

        return argv;
    }
}
