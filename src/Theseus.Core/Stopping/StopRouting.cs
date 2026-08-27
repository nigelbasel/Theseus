using Theseus.Core.Domain;

namespace Theseus.Core.Stopping;

/// <summary>
/// Chooses how a stop request reaches a session, from what that session is doing.
///
/// The distinction that matters is between a session that will take another turn on its own and one
/// that will not. A working session ends its turn soon and the hook is waiting there; an idle one
/// will sit for ever unless something starts a turn. A session waiting on its user is neither: it
/// is at a prompt, and a keystroke there answers a question rather than writing a message.
/// </summary>
public static class StopRouting
{
    /// <summary>The route for one session, given what it reports and whether a hook is installed.</summary>
    public static StopRoute For(SessionLifecycle lifecycle, bool hookInstalled)
    {
        if (lifecycle is not (SessionLifecycle.Running or SessionLifecycle.Idle
            or SessionLifecycle.NeedsAttention or SessionLifecycle.Launching))
        {
            return StopRoute.None;
        }

        if (!hookInstalled)
        {
            return StopRoute.PasteYourself;
        }

        return lifecycle switch
        {
            SessionLifecycle.NeedsAttention => StopRoute.AnswerItFirst,
            SessionLifecycle.Idle => StopRoute.Nudge,
            _ => StopRoute.WaitForTurnEnd,
        };
    }
}
