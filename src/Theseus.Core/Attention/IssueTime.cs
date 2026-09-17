namespace Theseus.Core.Attention;

/// <summary>How much time went on one issue over a range, on each clock, never added together.</summary>
/// <param name="Issue">The Jira issue.</param>
/// <param name="From">The start of the range.</param>
/// <param name="To">The end of the range.</param>
/// <param name="Attended">Time the person spent at a tab of a workstream while it was on this issue.</param>
/// <param name="Machine">Time sessions of a workstream spent working while it was on this issue.</param>
/// <param name="Workstreams">The workstreams that worked on it in the range.</param>
public sealed record IssueTime(
    string Issue,
    DateTimeOffset From,
    DateTimeOffset To,
    TimeSpan Attended,
    TimeSpan Machine,
    IReadOnlyList<string> Workstreams);
