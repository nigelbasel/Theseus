using Theseus.Core.Domain;

namespace Theseus.Core.Launching;

/// <summary>Everything a single workstream launch needs that is not the workstream itself.</summary>
/// <param name="Workstream">The workstream being started.</param>
/// <param name="Ticket">The single-use ticket the shim will redeem.</param>
/// <param name="SessionId">The identifier the session will run under.</param>
/// <param name="Mode">Whether this is a first run or a resume.</param>
/// <param name="ClaudeExecutablePath">The resolved Claude Code executable.</param>
/// <param name="TerminalExecutablePath">The resolved terminal executable.</param>
/// <param name="LauncherPath">The launcher shim the terminal starts in the tab.</param>
/// <param name="PipeName">The launch pipe the shim connects back on.</param>
/// <param name="TerminalWindowName">The named terminal window the tab joins.</param>
/// <param name="Preamble">Standing instructions put in front of the workstream own prompt.</param>
public sealed record WorkstreamLaunchRequest(
    Workstream Workstream,
    string Ticket,
    string SessionId,
    LaunchMode Mode,
    string ClaudeExecutablePath,
    string TerminalExecutablePath,
    string LauncherPath,
    string PipeName,
    string TerminalWindowName,
    string Preamble = "");
