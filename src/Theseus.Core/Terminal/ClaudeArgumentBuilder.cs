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

        var argv = new List<string>();

        // Headless runs speak stream-json both ways (Nigel, 2026-11-24, measured that day): the prompt goes in on stdin
        // as the first user message, so it is not positional.
        if (spec.Headless)
        {
            argv.AddRange(["-p", "--input-format", "stream-json", "--output-format", "stream-json", "--verbose"]);
        }

        argv.AddRange(
        [
            spec.Mode == LaunchMode.Resume ? "--resume" : "--session-id",
            spec.SessionId,
            // Sessions Theseus launches run unattended (Nigel, 2026-11-23); a prompt that still stops one shows on the
            // board as a wait for the owner.
            "--permission-mode",
            "auto",
        ]);

        if (spec.Headless && spec.MaxBudgetUsd is { } cap)
        {
            argv.Add("--max-budget-usd");
            // Down to the cent, so the cap never sits above what is left of a day.
            argv.Add((Math.Floor(cap * 100) / 100).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(spec.SessionName))
        {
            argv.Add("-n");
            argv.Add(spec.SessionName);
        }

        if (!spec.Headless && !string.IsNullOrWhiteSpace(spec.Prompt))
        {
            argv.Add(spec.Prompt);
        }

        if (spec.AddDirectories.Count > 0)
        {
            argv.Add("--add-dir");
            argv.AddRange(spec.AddDirectories);
        }

        if (!string.IsNullOrWhiteSpace(spec.Settings))
        {
            argv.Add("--settings");
            argv.Add(spec.Settings);
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
