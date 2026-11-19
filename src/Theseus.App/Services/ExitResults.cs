using Theseus.Core.Stopping;

namespace Theseus.App.Services;

/// <summary>What a nudge's outcome means for an /exit attempt, in Core's terms (close-step AC 9).</summary>
public static class ExitResults
{
    /// <summary>The /exit result for a nudge outcome.</summary>
    public static ExitResult From(NudgeOutcome outcome) => outcome switch
    {
        NudgeOutcome.Sent => ExitResult.Sent,
        NudgeOutcome.TabNotConfirmed => ExitResult.TabNotConfirmed,
        NudgeOutcome.FocusLost => ExitResult.FocusLost,
        NudgeOutcome.BlockedByElevation => ExitResult.Elevated,
        _ => ExitResult.Refused,
    };
}
