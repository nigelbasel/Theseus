using System.Text.RegularExpressions;

namespace Theseus.Core.Setup;

/// <summary>
/// The setup's tools step: Claude Code at the minimum Getting started names, Node and git found, and the agentic-factory
/// plugin installed. It reports and links to Getting started; it never installs anything (first-run-setup brief, out of scope).
/// </summary>
public sealed partial class ToolsCheck
{
    /// <summary>Asked of the version reader for Claude Code.</summary>
    public const string Claude = "claude";

    /// <summary>Asked of the version reader for Node.</summary>
    public const string Node = "node";

    /// <summary>Asked of the version reader for git.</summary>
    public const string Git = "git";

    /// <summary>The oldest Claude Code that refuses a tool call when a guard cannot run (getting-started.md, section 1).</summary>
    public static Version ClaudeMinimum { get; } = new(2, 1, 295);

    private readonly Func<string, string?> _versionOf;
    private readonly Func<bool> _pluginInstalled;

    /// <summary>Creates the check.</summary>
    /// <param name="versionOf">What a tool prints for <c>--version</c>, or null when it is not found.</param>
    /// <param name="pluginInstalled">Whether the agentic-factory plugin is installed and switched on.</param>
    public ToolsCheck(Func<string, string?> versionOf, Func<bool> pluginInstalled)
    {
        _versionOf = versionOf;
        _pluginInstalled = pluginInstalled;
    }

    /// <summary>One finding per tool, in the order Getting started lists them.</summary>
    public IReadOnlyList<ToolFinding> Run() =>
    [
        ClaudeFinding(_versionOf(Claude)),
        Found("Node", _versionOf(Node), "Node was not found on the PATH: without it every tool call is refused"),
        Found("git", _versionOf(Git), "git was not found on the PATH"),
        _pluginInstalled()
            ? new ToolFinding("agentic-factory plugin", true, "The agentic-factory plugin is installed")
            : new ToolFinding("agentic-factory plugin", false, "The agentic-factory plugin is not installed"),
    ];

    private static ToolFinding ClaudeFinding(string? output)
    {
        if (output is null)
        {
            return new ToolFinding("Claude Code", false, "Claude Code was not found");
        }

        if (VersionText().Match(output) is not { Success: true } match || !Version.TryParse(match.Value, out var version))
        {
            return new ToolFinding("Claude Code", false, $"Claude Code's version could not be read; {ClaudeMinimum} or later is needed");
        }

        return version >= ClaudeMinimum
            ? new ToolFinding("Claude Code", true, $"Claude Code {version}")
            : new ToolFinding("Claude Code", false, $"Claude Code {version} is older than {ClaudeMinimum}: run claude update");
    }

    private static ToolFinding Found(string name, string? output, string missing) =>
        output is null
            ? new ToolFinding(name, false, missing)
            : new ToolFinding(name, true, $"{name} {VersionText().Match(output).Value}".TrimEnd());

    [GeneratedRegex(@"\d+(\.\d+){1,3}")]
    private static partial Regex VersionText();
}
