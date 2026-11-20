namespace Theseus.Core.Projects;

/// <summary>
/// The mark and word each stage state is shown with, so state is never colour alone (1.1d, Words and marks; Nigel
/// accepted the mockup, 2026-11-20).
/// </summary>
public static class StageNodeStates
{
    /// <summary>The state's mark.</summary>
    public static string Mark(StageNodeState state) => state switch
    {
        StageNodeState.Done => "✓",
        StageNodeState.WaitingOnYou => "◆",
        StageNodeState.Busy => "●",
        StageNodeState.Stuck => "⚠",
        StageNodeState.HoldoutsNotRun => "◐",
        _ => "–",
    };

    /// <summary>The state's word.</summary>
    public static string Word(StageNodeState state) => state switch
    {
        StageNodeState.Done => "Done",
        StageNodeState.WaitingOnYou => "Waiting on you",
        StageNodeState.Busy => "Busy",
        StageNodeState.Stuck => "Stuck",
        StageNodeState.HoldoutsNotRun => "Holdouts not run yet",
        _ => "To do",
    };
}
