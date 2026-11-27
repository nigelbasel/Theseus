namespace Theseus.Core.Attention;

/// <summary>
/// A dated change in how time is measured (7.3), kept in <c>attention-methods.jsonl</c>, so a report spanning it says
/// "measurement changed on" that date rather than showing a false jump.
/// </summary>
/// <param name="Id">Which change: one line per id, ever.</param>
/// <param name="At">When this machine first measured the new way.</param>
/// <param name="Change">What changed, in a sentence a report can show.</param>
public sealed record MethodBreak(string Id, DateTimeOffset At, string Change);
