using System.Globalization;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Stopping;

/// <summary>
/// The record each stop leaves in the Theseus log: how long it took when it finished, and how far it got when
/// it did not (stop-timing, spec approved by Nigel 2026-11-18). It exists to measure stopping from data rather
/// than guess at it, and to show where a stuck stop stuck. It records only; it never changes how a stop runs.
/// </summary>
public static class StopTiming
{
    /// <summary>How long a stop took, from the times recorded for it.</summary>
    /// <param name="request">The delivered request.</param>
    /// <param name="handoffWrittenUtc">When the handoff was last written, or null if that cannot be read.</param>
    /// <param name="exitUtc">When /exit was typed.</param>
    public static StopDurations Finished(PendingStopRequest request, DateTimeOffset? handoffWrittenUtc, DateTimeOffset exitUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        var delivered = request.DeliveredUtc;

        return new StopDurations(
            exitUtc - request.RequestedUtc,
            delivered - request.RequestedUtc,
            handoffWrittenUtc - delivered,
            handoffWrittenUtc - request.RequestedUtc,
            exitUtc - handoffWrittenUtc);
    }

    /// <summary>
    /// How far a stop got. A handoff counts only if it was written at or after the request, the same rule
    /// <see cref="SessionClosing"/> uses, so yesterday's handoff is not mistaken for an answer.
    /// </summary>
    /// <param name="request">The request being removed.</param>
    /// <param name="delivered">Whether the hook had handed it to the session.</param>
    /// <param name="handoffWrittenUtc">When the handoff was last written, or null if there is none.</param>
    public static StopProgress Progress(PendingStopRequest request, bool delivered, DateTimeOffset? handoffWrittenUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!delivered)
        {
            return StopProgress.NeverDelivered;
        }

        return handoffWrittenUtc is { } written && written >= request.RequestedUtc
            ? StopProgress.HandedOverNotClosed
            : StopProgress.DeliveredNoHandoff;
    }

    /// <summary>How far a stop got, in words for the log.</summary>
    public static string Describe(StopProgress progress) => progress switch
    {
        StopProgress.NeverDelivered => "never delivered: the session did not end a turn",
        StopProgress.DeliveredNoHandoff => "delivered, but no handoff was written",
        _ => "handed over, but /exit was never typed",
    };

    /// <summary>
    /// What a finished stop's log line says after the workstream's name. Each missing time is named as missing
    /// on its own, so a handoff time that could not be read is never reported as a missing delivery time.
    /// </summary>
    public static string FinishedText(StopDurations durations)
    {
        ArgumentNullException.ThrowIfNull(durations);

        var delivery = durations.RequestToDelivery is { } toDelivery
            ? $"delivered after {Seconds(toDelivery)}"
            : "delivery time not recorded";

        var handoff = (durations.RequestToDelivery, durations.DeliveryToHandoff, durations.RequestToHandoff, durations.HandoffToExit) switch
        {
            ({ }, { } toHandoff, _, { } toExit) => $", handoff {Seconds(toHandoff)} later, /exit {Seconds(toExit)} after that",
            (null, _, { } fromRequest, { } toExit) => $", handoff {Seconds(fromRequest)} after the request, /exit {Seconds(toExit)} after that",
            _ => ", handoff time could not be read",
        };

        var negative = new[] { durations.Total, durations.RequestToDelivery, durations.DeliveryToHandoff, durations.RequestToHandoff, durations.HandoffToExit }
            .Any(span => span < TimeSpan.Zero)
            ? " (a negative figure means the clock or a file time moved)"
            : string.Empty;

        return $"finished in {Seconds(durations.Total)}: {delivery}{handoff}{negative}";
    }

    /// <summary>What an unfinished stop's log line says after the workstream's name.</summary>
    public static string UnfinishedText(StopRemoval removal, TimeSpan age, StopProgress progress)
    {
        var why = removal switch
        {
            StopRemoval.StartedAgain => "its card was started again",
            StopRemoval.NewerSession => "a newer session is running",
            StopRemoval.Declined => "declined when the board opened",
            _ => "it expired",
        };

        return $"did not finish ({why}, {Age(age)} after it was asked): {Describe(progress)}";
    }

    /// <summary>
    /// An age in a unit that reads naturally: whole seconds under 90 seconds, whole minutes under 90 minutes,
    /// hours to one decimal after that. Truncated, never rounded up, so 59.9 minutes is not shown as an hour.
    /// </summary>
    public static string Age(TimeSpan age) =>
        age.TotalSeconds < 90 ? $"{(long)age.TotalSeconds}s"
        : age.TotalMinutes < 90 ? $"{(long)age.TotalMinutes}m"
        : $"{(Math.Floor(age.TotalHours * 10) / 10).ToString("0.0", CultureInfo.InvariantCulture)}h";

    /// <summary>
    /// Logs how long a stop took, at the moment /exit is typed. <paramref name="handoffWrittenUtc"/> is the handoff
    /// time the close was decided on, passed in rather than read again.
    /// </summary>
    public static void ReportFinished(PendingStopRequest request, DateTimeOffset? handoffWrittenUtc, DateTimeOffset exitUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        TheseusLog.Information("Stop of {Workstream} {Outcome}",
            request.WorkstreamKey, FinishedText(Finished(request, handoffWrittenUtc, exitUtc)));
    }

    /// <summary>Logs how far a stop got, at the moment its request is removed without the session being closed.</summary>
    public static void ReportUnfinished(PendingStopRequest request, bool delivered, StopRemoval removal, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        TheseusLog.Information("Stop of {Workstream} {Outcome}",
            request.WorkstreamKey,
            UnfinishedText(removal, nowUtc - request.RequestedUtc, Progress(request, delivered, HandoffWrittenUtc(request))));
    }

    /// <summary>
    /// When a handoff was last written, or null if there is no path, no file, or the time cannot be read. The one
    /// place this is read, so the time the close step decides on and the time the log reports are the same
    /// (stop-timing review, F2).
    /// </summary>
    public static DateTimeOffset? HandoffWrittenUtc(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            return File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException
                                              or NotSupportedException)
        {
            return null;
        }
    }

    private static DateTimeOffset? HandoffWrittenUtc(PendingStopRequest request) => HandoffWrittenUtc(request.HandoffPath);

    private static string Seconds(TimeSpan span) =>
        $"{span.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture)}s";
}
