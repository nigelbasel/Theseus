namespace Theseus.Core.Running;

/// <summary>The run started.</summary>
/// <param name="SessionId">The Claude Code session it runs under.</param>
/// <param name="Mode">Whether it opened in a tab or headless.</param>
public sealed record RunStarted(string SessionId, RunMode Mode) : RunRequestOutcome;
