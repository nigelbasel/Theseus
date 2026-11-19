namespace Theseus.Core.Stopping;

/// <summary>
/// Where one stop under way has got to, for its card: the stage reached, when, and why /exit failed if it did
/// (close-step AC 5 and 6). Built only from what is already recorded: the request on disk, the handoff's written time,
/// and the last /exit attempt.
/// </summary>
/// <param name="Stage">The latest stage reached.</param>
/// <param name="AtUtc">When it was reached.</param>
/// <param name="Failure">For <see cref="StopStage.ExitFailed"/>, what the attempt did.</param>
public sealed record StopState(StopStage Stage, DateTimeOffset AtUtc, ExitResult? Failure = null)
{
    /// <summary>The stop's state, or null when no stop is under way.</summary>
    /// <param name="waiting">A request written and not yet collected.</param>
    /// <param name="delivered">A request handed to the session.</param>
    /// <param name="handoffWrittenUtc">When the handoff was last written, if it exists.</param>
    /// <param name="lastAttempt">The last /exit attempt for the delivered request.</param>
    public static StopState? Of(
        PendingStopRequest? waiting,
        PendingStopRequest? delivered,
        DateTimeOffset? handoffWrittenUtc,
        ExitAttempt? lastAttempt)
    {
        if (delivered is null)
        {
            return waiting is null ? null : new StopState(StopStage.Asked, waiting.RequestedUtc);
        }

        if (lastAttempt is not null)
        {
            return lastAttempt.Result == ExitResult.Sent
                ? new StopState(StopStage.ExitTyped, lastAttempt.AtUtc)
                : new StopState(StopStage.ExitFailed, lastAttempt.AtUtc, lastAttempt.Result);
        }

        if (handoffWrittenUtc is { } written && written >= delivered.RequestedUtc)
        {
            return new StopState(StopStage.HandoffWritten, written);
        }

        return new StopState(StopStage.HandedOver, delivered.DeliveredUtc ?? delivered.RequestedUtc);
    }
}
