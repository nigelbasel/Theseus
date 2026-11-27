namespace Theseus.Core.Locks;

/// <summary>How a run held through the lock ended, against the commit and command it ran, for telling a repeat apart.</summary>
public sealed record RunResult(string? Card, string? Commit, string? Command, int ExitCode, DateTimeOffset WhenUtc, string? Who = null);
