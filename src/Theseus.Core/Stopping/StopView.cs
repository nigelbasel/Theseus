namespace Theseus.Core.Stopping;

/// <summary>
/// What one card shows for its session's stop: how far it has got, whether it was held back when the board opened,
/// and whether Close now is offered (close-step AC 5, 6 and 9). Decided here rather than in the window so it can be
/// tested (verify part 1, F2).
/// </summary>
/// <param name="State">How far the stop has got, or null when none is under way.</param>
/// <param name="HeldBack">Whether it was held back when the board opened, and closes only once the owner agrees.</param>
/// <param name="CanCloseNow">Whether Close now is offered.</param>
public sealed record StopView(StopState? State, bool HeldBack, bool CanCloseNow)
{
    /// <summary>The view of one live session's stop.</summary>
    /// <param name="waiting">A request written and not yet collected.</param>
    /// <param name="delivered">A request handed to the session.</param>
    /// <param name="handoffWrittenUtc">When the handoff was last written, if it exists.</param>
    /// <param name="exits">What Theseus has typed /exit for.</param>
    /// <param name="sessionId">The session.</param>
    /// <param name="sessionStartedUtc">When the run of it now running started.</param>
    /// <param name="heldBack">Whether its stop was held back when the board opened.</param>
    public static StopView Of(
        PendingStopRequest? waiting,
        PendingStopRequest? delivered,
        DateTimeOffset? handoffWrittenUtc,
        ExitsTyped exits,
        string sessionId,
        DateTimeOffset sessionStartedUtc,
        bool heldBack)
    {
        ArgumentNullException.ThrowIfNull(exits);

        if (waiting is null && delivered is null)
        {
            return exits.Closing(sessionId, sessionStartedUtc) is { } sent
                ? new StopView(new StopState(StopStage.ExitTyped, sent.AtUtc), false, false)
                : new StopView(null, false, false);
        }

        var state = StopState.Of(waiting, delivered, handoffWrittenUtc, exits.LastAttempt(delivered));

        return new StopView(state, heldBack,
            !heldBack && state?.Stage == StopStage.ExitFailed && exits.MayCloseNow(delivered));
    }
}
