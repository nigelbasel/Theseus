namespace Theseus.Core.Stopping;

/// <summary>
/// What the card's stopping icon says, in the words of the close-step spec (AC 6 and 9, approved by Nigel 2026-11-19).
/// A failed /exit says why and what to do: Close now only where nothing was typed for certain.
/// </summary>
public static class StopStageText
{
    /// <summary>The tooltip for a stop at this state, with its time in the given zone.</summary>
    public static string For(StopState state, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(zone);

        var at = TimeZoneInfo.ConvertTime(state.AtUtc, zone).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);

        return state.Stage switch
        {
            StopStage.Asked => $"Asked at {at}. Waiting for its turn to end.",
            StopStage.HandedOver => $"Handed over at {at}. Writing its handoff.",
            StopStage.HandoffWritten => $"Handoff written at {at}. Closes once it has been quiet for {SessionClosing.Quiet.TotalSeconds:0} s.",
            StopStage.ExitTyped => $"Typed /exit at {at}. Closing.",
            _ => $"Could not type /exit: {Reason(state.Failure)}",
        };
    }

    /// <summary>
    /// The tooltip for a card's stop. A stop held back when the board opened says so, since it does not close after
    /// its quiet period until the owner agrees (close-step verify F7).
    /// </summary>
    public static string For(StopView view, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (view.State is null)
        {
            return string.Empty;
        }

        return view.HeldBack && view.State.Stage is not (StopStage.ExitTyped or StopStage.ExitFailed)
            ? "Held back when the board opened. Close them, in the banner, finishes this stop."
            : For(view.State, zone);
    }

    private static string Reason(ExitResult? failure) => failure switch
    {
        ExitResult.TabNotConfirmed => "its tab could not be confirmed. Close now tries again.",
        ExitResult.FocusLost => "the terminal was not in front. Close now tries again.",
        ExitResult.Elevated => "the terminal runs as administrator and Theseus does not. Close it yourself.",
        _ => "Windows refused the keystrokes. Close it yourself.",
    };
}
