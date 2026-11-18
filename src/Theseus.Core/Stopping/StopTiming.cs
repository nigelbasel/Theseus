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

    /// <summary>Logs how long a stop took, at the moment /exit is typed.</summary>
    public static void ReportFinished(PendingStopRequest request, DateTimeOffset exitUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        var durations = Finished(request, HandoffWrittenUtc(request), exitUtc);

        if (durations.RequestToDelivery is { } toDelivery && durations.DeliveryToHandoff is { } toHandoff
            && durations.HandoffToExit is { } toExit)
        {
            TheseusLog.Information(
                "Stop of {Workstream} finished in {Total}s: delivered after {ToDelivery}s, handoff {ToHandoff}s later, /exit {ToExit}s after that",
                request.WorkstreamKey, Seconds(durations.Total), Seconds(toDelivery), Seconds(toHandoff), Seconds(toExit));

            return;
        }

        TheseusLog.Information(
            "Stop of {Workstream} finished in {Total}s: delivery time not recorded, handoff {RequestToHandoff}s after the request, /exit {ToExit}s after that",
            request.WorkstreamKey, Seconds(durations.Total), Seconds(durations.RequestToHandoff), Seconds(durations.HandoffToExit));
    }

    /// <summary>Logs how far a stop got, at the moment its request is removed without the session being closed.</summary>
    public static void ReportUnfinished(PendingStopRequest request, bool delivered, StopRemoval removal, DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(request);

        TheseusLog.Information(
            "Stop of {Workstream} did not finish ({Removal}, {Age} after it was asked): {Progress}",
            request.WorkstreamKey, removal, Age(nowUtc - request.RequestedUtc),
            Describe(Progress(request, delivered, HandoffWrittenUtc(request))));
    }

    private static DateTimeOffset? HandoffWrittenUtc(PendingStopRequest request)
    {
        try
        {
            return request.HandoffPath is { } path && File.Exists(path)
                ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero)
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }

    private static string Seconds(TimeSpan? span) =>
        span is { } value ? value.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture) : "(not recorded)";

    private static string Age(TimeSpan span) =>
        span.TotalHours >= 1
            ? $"{span.TotalHours.ToString("0.0", CultureInfo.InvariantCulture)}h"
            : $"{span.TotalMinutes.ToString("0.0", CultureInfo.InvariantCulture)}m";
}
