namespace Theseus.Core.Launching;

/// <summary>
/// What travels down the launch pipe to the shim. Nothing here ever reaches a command line the
/// terminal parses, which is why titles, prompts and paths need no escaping on this path.
/// </summary>
/// <param name="SessionId">The session identifier the tab runs under.</param>
/// <param name="Mode">Whether this is a first run or a resume.</param>
/// <param name="ClaudeExecutablePath">The resolved path to the Claude Code executable.</param>
/// <param name="WorkingDirectory">The directory the session starts in.</param>
/// <param name="SessionName">The display name given to Claude Code with <c>-n</c>.</param>
/// <param name="Prompt">The opening prompt.</param>
/// <param name="AddDirectories">Extra directories added to the session's scope.</param>
public sealed record LaunchSpec(
    string SessionId,
    LaunchMode Mode,
    string ClaudeExecutablePath,
    string WorkingDirectory,
    string SessionName,
    string Prompt,
    IReadOnlyList<string> AddDirectories);
