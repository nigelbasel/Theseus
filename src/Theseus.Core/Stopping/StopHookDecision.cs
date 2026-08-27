namespace Theseus.Core.Stopping;

/// <summary>
/// Whether a hook should hand a session the request waiting for it.
///
/// A hook runs on every turn of every session on the machine, including sessions Theseus knows
/// nothing about, so silence is the default and every reason for it is enumerated here rather than
/// discovered at the call site.
/// </summary>
public static class StopHookDecision
{
    /// <summary>The request to deliver, or null to say nothing at all.</summary>
    public static PendingStopRequest? Deliver(
        StopHookInput? input,
        PendingStopRequest? waiting,
        DateTimeOffset now)
    {
        if (input is null || waiting is null)
        {
            return null;
        }

        if (input.StopHookActive || !string.IsNullOrWhiteSpace(input.AgentId))
        {
            return null;
        }

        return waiting.ExpiresUtc > now ? waiting : null;
    }
}
