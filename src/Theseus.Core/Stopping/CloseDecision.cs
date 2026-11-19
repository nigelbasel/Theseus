namespace Theseus.Core.Stopping;

/// <summary>The close step's decision for one session.</summary>
/// <param name="Verdict">What to do.</param>
/// <param name="QuietEndsUtc">For <see cref="CloseVerdict.Wait"/>, when the session's quiet period ends; otherwise null.</param>
public sealed record CloseDecision(CloseVerdict Verdict, DateTimeOffset? QuietEndsUtc = null);
