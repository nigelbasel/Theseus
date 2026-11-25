namespace Theseus.Core.Projects;

/// <summary>
/// The mark, word and meaning each state is shown with, so state is never colour alone (1.1d, Words and marks; 1.1f F31,
/// J49).
/// </summary>
public static class StageNodeStates
{
    /// <summary>The state's mark.</summary>
    public static string Mark(StageNodeState state) => state switch
    {
        StageNodeState.Done or StageNodeState.DoneNoRecord or StageNodeState.Answered => "✓",
        StageNodeState.WaitingOnYou => "◆",
        StageNodeState.Busy => "●",
        StageNodeState.Stuck => "⚠",
        StageNodeState.HoldoutsNotRun => "◐",
        StageNodeState.HandedOn => "↳",
        StageNodeState.NotNeeded => "·",
        StageNodeState.RecordsElsewhere => "…",
        StageNodeState.NotInProject => "+",
        _ => "–",
    };

    /// <summary>The state's word.</summary>
    public static string Word(StageNodeState state) => state switch
    {
        StageNodeState.Done => "Done",
        StageNodeState.WaitingOnYou => "Waiting for you",
        StageNodeState.Busy => "Busy",
        StageNodeState.Stuck => "Stuck",
        StageNodeState.HoldoutsNotRun => "Built, not yet checked",
        StageNodeState.HandedOn => "Handed on",
        StageNodeState.NotNeeded => "Not needed",
        StageNodeState.DoneNoRecord => "Done, no record",
        StageNodeState.RecordsElsewhere => "Records kept elsewhere",
        StageNodeState.Answered => "Answered, criteria not ticked",
        StageNodeState.NotInProject => "Not in project.md",
        _ => "Not started",
    };

    /// <summary>What the state means, the first of its three answers (1.1f F31).</summary>
    public static string Meaning(StageNodeState state) => state switch
    {
        StageNodeState.Done => "Finished: its record is written and any gate you accepted.",
        StageNodeState.WaitingOnYou => "Waiting for you: nothing moves until you act.",
        StageNodeState.Busy => "A session is working on it now.",
        StageNodeState.Stuck => "Its evidence failed, or its card is blocked.",
        StageNodeState.HoldoutsNotRun => "Built and tested by its builder, not yet checked independently. The independent check runs "
                                         + "scenarios written by a session that did not build it and hidden from the builder (holdout "
                                         + "scenarios). Only you start one.",
        StageNodeState.HandedOn => "Done by the pieces this one handed work to.",
        StageNodeState.NotNeeded => "Skipped, with its reason: nothing was needed here.",
        StageNodeState.DoneNoRecord => "Done before the stage after it, but nothing recorded in the records folder.",
        StageNodeState.RecordsElsewhere => "Its records are not in the project.",
        StageNodeState.Answered => "Reported back and you confirmed it; its objective's criteria are not yet ticked.",
        StageNodeState.NotInProject => "A linked card that no row of the project file lists.",
        _ => "No work recorded for this stage.",
    };

    /// <summary>Whether a stage in this state needs nothing more before the piece is finished.</summary>
    public static bool IsFinished(StageNodeState state) => state is StageNodeState.Done or StageNodeState.DoneNoRecord
        or StageNodeState.NotNeeded or StageNodeState.HandedOn or StageNodeState.RecordsElsewhere or StageNodeState.Answered;
}
