namespace Theseus.Core.Stopping;

/// <summary>
/// Whether a session that was asked to stop has finished, and can be closed.
///
/// The stop request has always ended "Then exit", and no session has ever been able to do it: a
/// Claude Code session cannot terminate its own process. Every stopped session therefore sat in its
/// tab with its handover written and nothing left to do, and somebody closed it by hand.
///
/// So Theseus closes it, by typing what a person would type. Nothing is killed - a killed process
/// writes nothing, which is the whole reason stopping exists - and the session exits the ordinary
/// way, which lets the launcher finish and the tab close on its own.
///
/// The conditions are deliberately strict, because closing a tab that had more to say destroys work
/// and there is no undo. All four must hold.
/// </summary>
public static class SessionClosing
{
    /// <summary>What is typed into the session to end it, exactly as a person would.</summary>
    public const string ExitCommand = "/exit";

    /// <summary>
    /// How long a session must have been idle before it is closed. Long enough that a session
    /// pausing to think is not mistaken for one that has finished.
    /// </summary>
    public static readonly TimeSpan Quiet = TimeSpan.FromSeconds(20);

    /// <summary>
    /// How soon after a stop is handed over the turn that took it must end, for a handover written earlier in the run to
    /// count. A turn that ends later is someone working with the session, not its answer to the stop.
    /// </summary>
    public static readonly TimeSpan AnswerWindow = TimeSpan.FromMinutes(10);

    /// <summary>Whether this session should now be told to exit.</summary>
    /// <param name="delivered">The stop request this session was handed, if any.</param>
    /// <param name="handoffWrittenUtc">When the handover document was last written, if it exists.</param>
    /// <param name="status">What the session reports it is doing.</param>
    /// <param name="statusSinceUtc">When it last changed to that.</param>
    /// <param name="now">The current moment.</param>
    /// <param name="alreadyAsked">Whether this session has already been told to exit.</param>
    /// <param name="sessionStartedUtc">
    /// When the run now going started, if known. A handover this run wrote before the stop counts once the turn that took
    /// the stop has ended: a session whose handover is already current says so and leaves the file as it is.
    /// </param>
    public static bool ShouldClose(
        PendingStopRequest? delivered,
        DateTimeOffset? handoffWrittenUtc,
        string? status,
        DateTimeOffset? statusSinceUtc,
        DateTimeOffset now,
        bool alreadyAsked,
        DateTimeOffset? sessionStartedUtc = null)
    {
        // Only a session Theseus asked to stop, and only once. Asking twice would type /exit into a
        // session that had already begun something else.
        if (delivered is null || alreadyAsked)
        {
            return false;
        }

        // Idle, and not merely quiet. A session that is working has not finished; one that is
        // waiting has asked a question, and closing it would throw the question away unanswered.
        if (!string.Equals(status, "idle", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // The handover has to exist and be this stop's, or this run's when the stop's own turn left it as it was; never one
        // left over from a previous run.
        // Without that check a session asked to hand over and failing to would be closed anyway,
        // losing exactly what stopping is for.
        if (handoffWrittenUtc is not { } written)
        {
            return false;
        }

        var handed = delivered.DeliveredUtc ?? delivered.RequestedUtc;
        var currentBeforeTheStop = sessionStartedUtc is { } started && written >= started
                                   && statusSinceUtc is { } idleSince && idleSince >= handed && idleSince <= handed + AnswerWindow;

        if (written < delivered.RequestedUtc && !currentBeforeTheStop)
        {
            return false;
        }

        return statusSinceUtc is not { } since || now - since >= Quiet;
    }

    /// <summary>
    /// What the close step does with one session. A stop handed to an earlier run of the session is retired, judged by
    /// when it was handed over, the same rule the Stop press uses (close-step AC 8; resumed-stop review S2). A session
    /// that meets every condition but quiet is waited for: the refresh its own change to idle caused always comes
    /// before its quiet ends, and an idle session writes nothing more, so without a wait nothing checks again until
    /// something unrelated refreshes the board (close-step AC 1; seen as closes 87 s to 11 minutes late).
    /// </summary>
    /// <param name="delivered">The stop request this session was handed, if any.</param>
    /// <param name="sessionStartedUtc">When the session running now started.</param>
    /// <param name="handoffWrittenUtc">When the handover document was last written, if it exists.</param>
    /// <param name="status">What the session reports it is doing.</param>
    /// <param name="statusSinceUtc">When it last changed to that.</param>
    /// <param name="now">The current moment.</param>
    /// <param name="alreadyAsked">Whether /exit has already been tried for this stop.</param>
    public static CloseDecision Decide(
        PendingStopRequest? delivered,
        DateTimeOffset sessionStartedUtc,
        DateTimeOffset? handoffWrittenUtc,
        string? status,
        DateTimeOffset? statusSinceUtc,
        DateTimeOffset now,
        bool alreadyAsked)
    {
        if (delivered is null)
        {
            return new CloseDecision(CloseVerdict.Skip);
        }

        if (HandedBefore(delivered, sessionStartedUtc))
        {
            return new CloseDecision(CloseVerdict.Retire);
        }

        if (ShouldClose(delivered, handoffWrittenUtc, status, statusSinceUtc, now, alreadyAsked, sessionStartedUtc))
        {
            return new CloseDecision(CloseVerdict.Close);
        }

        if (statusSinceUtc is { } since
            && ShouldClose(delivered, handoffWrittenUtc, status, statusSinceUtc, since + Quiet, alreadyAsked, sessionStartedUtc))
        {
            return new CloseDecision(CloseVerdict.Wait, since + Quiet);
        }

        return new CloseDecision(CloseVerdict.Skip);
    }

    /// <summary>
    /// Whether the session running now started after the stop was asked, and so cannot be the session
    /// that was asked. A resumed session keeps its identifier, so the identifier alone does not say
    /// which process a request was for; the process's start does.
    /// </summary>
    public static bool StartedSince(PendingStopRequest request, DateTimeOffset sessionStartedUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        return sessionStartedUtc > request.RequestedUtc;
    }

    /// <summary>
    /// Whether a delivered stop was handed to an earlier run of the session, not to the one now running. The hook
    /// hands a waiting request to whichever process has the id, so the moment it was handed over decides, not the
    /// moment it was asked; a request with no delivery time falls back to when it was asked (resumed-stop review S2).
    /// </summary>
    public static bool HandedBefore(PendingStopRequest request, DateTimeOffset sessionStartedUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        return sessionStartedUtc > (request.DeliveredUtc ?? request.RequestedUtc);
    }
}
