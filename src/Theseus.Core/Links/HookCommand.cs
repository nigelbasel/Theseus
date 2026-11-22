namespace Theseus.Core.Links;

/// <summary>A PreToolUse hook command in Claude Code's settings.</summary>
/// <param name="Command">The command line.</param>
/// <param name="Everything">Whether its matcher runs it before every tool, as the holdout guard must be run.</param>
public sealed record HookCommand(string Command, bool Everything);
