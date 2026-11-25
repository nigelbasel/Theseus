using Theseus.Core.Domain;

namespace Theseus.Core.Links;

/// <summary>
/// A Chains row's state and the rules the panel's filters use (chains-panel-at-scale criteria 8 and 12). Active is the
/// board's own Show active only rule (Nigel, 2026-11-24), so the one word means one thing in both places; completed is
/// Done or archived, never merely stopped (Nigel, 2026-11-25).
/// </summary>
public static class ChainRowStates
{
    /// <summary>The first state that applies, in the order of <see cref="ChainRowState"/>.</summary>
    public static ChainRowState Of(ChainRowFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return facts switch
        {
            { Lifecycle: SessionLifecycle.NeedsAttention } => ChainRowState.AskingYou,
            { ReportWaiting: true } => ChainRowState.Reported,
            { ReportLost: true } => ChainRowState.ReportLost,
            { StartAsked: true } => ChainRowState.StartAsked,
            { Lifecycle: SessionLifecycle.Launching } => ChainRowState.Starting,
            { Lifecycle: SessionLifecycle.Running } => ChainRowState.Running,
            { Lifecycle: SessionLifecycle.Idle } => ChainRowState.Idle,
            { IsArchived: true } => ChainRowState.Archived,
            { IsPaused: true } => ChainRowState.Paused,
            { IsBlocked: true } => ChainRowState.Blocked,
            { IsDone: true } => ChainRowState.Done,
            { Readiness: CardReadiness.Ready } => ChainRowState.Ready,
            { Readiness: CardReadiness.Waiting } => ChainRowState.WaitingOnInputs,
            _ => ChainRowState.Stopped,
        };
    }

    /// <summary>The state's mark; the session marks are the board's own (<see cref="SessionMark"/>).</summary>
    public static string Mark(ChainRowState state) => state switch
    {
        ChainRowState.AskingYou => "◆",
        ChainRowState.Reported => "↩",
        ChainRowState.ReportLost => "!",
        ChainRowState.StartAsked => "⏵",
        ChainRowState.Starting => "◌",
        ChainRowState.Running => "●",
        ChainRowState.Idle => "○",
        ChainRowState.Archived => "▣",
        ChainRowState.Paused => "⏸",
        ChainRowState.Blocked => "⛔",
        ChainRowState.Done => "✓",
        ChainRowState.Ready => "▶",
        ChainRowState.WaitingOnInputs => "⧗",
        _ => "■",
    };

    /// <summary>The state's word.</summary>
    public static string Word(ChainRowState state) => state switch
    {
        ChainRowState.AskingYou => "asking you",
        ChainRowState.Reported => "reported",
        ChainRowState.ReportLost => "report lost",
        ChainRowState.StartAsked => "start asked",
        ChainRowState.Starting => "starting",
        ChainRowState.Running => "running",
        ChainRowState.Idle => "idle",
        ChainRowState.Archived => "archived",
        ChainRowState.Paused => "paused",
        ChainRowState.Blocked => "blocked",
        ChainRowState.Done => "done",
        ChainRowState.Ready => "ready",
        ChainRowState.WaitingOnInputs => "waiting on inputs",
        _ => "stopped",
    };

    /// <summary>Whether something on the card waits on the owner: a question, a report to confirm or lost, or a start to allow.</summary>
    public static bool WaitsOnOwner(ChainRowFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return facts.Lifecycle == SessionLifecycle.NeedsAttention || facts.ReportWaiting || facts.ReportLost || facts.StartAsked;
    }

    /// <summary>Whether it has a session, or something waiting on the owner.</summary>
    public static bool IsActive(ChainRowFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return HasSession(facts) || WaitsOnOwner(facts);
    }

    /// <summary>Whether it is finished with: Done or archived, with no session and nothing waiting on the owner.</summary>
    public static bool IsCompleted(ChainRowFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return (facts.IsDone || facts.IsArchived) && !IsActive(facts);
    }

    private static bool HasSession(ChainRowFacts facts) => facts.Lifecycle
        is SessionLifecycle.Launching or SessionLifecycle.Running or SessionLifecycle.Idle or SessionLifecycle.NeedsAttention;
}
