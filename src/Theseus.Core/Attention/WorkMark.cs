namespace Theseus.Core.Attention;

/// <summary>
/// A session saying what it is working on. It carries a moment, stamped by Theseus, and never a
/// duration: how long is measured, not reported.
/// </summary>
/// <param name="SessionId">The session that said it.</param>
/// <param name="Workstream">The workstream that session belongs to.</param>
/// <param name="Kind">Start or stop.</param>
/// <param name="Issue">The Jira issue started on; null for a stop.</param>
/// <param name="Summary">One line on what the session is doing, if it gave one.</param>
/// <param name="At">When Theseus received it.</param>
public sealed record WorkMark(
    string SessionId,
    string Workstream,
    WorkMarkKind Kind,
    string? Issue,
    string? Summary,
    DateTimeOffset At);
