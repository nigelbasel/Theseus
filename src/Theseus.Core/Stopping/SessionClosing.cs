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

    /// <summary>Whether this session should now be told to exit.</summary>
    /// <param name="delivered">The stop request this session was handed, if any.</param>
    /// <param name="handoffWrittenUtc">When the handover document was last written, if it exists.</param>
    /// <param name="status">What the session reports it is doing.</param>
    /// <param name="statusSinceUtc">When it last changed to that.</param>
    /// <param name="now">The current moment.</param>
    /// <param name="alreadyAsked">Whether this session has already been told to exit.</param>
    public static bool ShouldClose(
        PendingStopRequest? delivered,
        DateTimeOffset? handoffWrittenUtc,
        string? status,
        DateTimeOffset? statusSinceUtc,
        DateTimeOffset now,
        bool alreadyAsked)
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

        // The handover has to exist and be this stop's, not one left over from a previous day.
        // Without that check a session asked to hand over and failing to would be closed anyway,
        // losing exactly what stopping is for.
        if (handoffWrittenUtc is not { } written || written < delivered.RequestedUtc)
        {
            return false;
        }

        return statusSinceUtc is not { } since || now - since >= Quiet;
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
}
