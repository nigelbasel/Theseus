namespace Theseus.Launcher;

/// <summary>What became of the session the shim was asked to run.</summary>
/// <param name="SessionId">The identifier the session finally ran under, which differs from the
/// requested one when a failed resume was reminted.</param>
/// <param name="ExitCode">Claude Code's final exit code.</param>
/// <param name="HoldTab">Whether the tab should stay open so the operator can read the failure.</param>
public sealed record ClaudeRunResult(string SessionId, int ExitCode, bool HoldTab);
