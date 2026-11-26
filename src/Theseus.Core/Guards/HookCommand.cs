namespace Theseus.Core.Guards;

/// <summary>A PreToolUse hook command in Claude Code's settings.</summary>
/// <param name="Command">The command line.</param>
/// <param name="Matcher">Its entry's matcher, the tools it runs before; null when the entry has none, which is every tool.</param>
public sealed record HookCommand(string Command, string? Matcher);
