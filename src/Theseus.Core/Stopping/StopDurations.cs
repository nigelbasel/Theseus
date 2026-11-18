namespace Theseus.Core.Stopping;

/// <summary>
/// How long a finished stop took. A figure is null when a time it needs was not recorded; it is never filled
/// in by guessing.
/// </summary>
/// <param name="Total">From the request to /exit being typed.</param>
/// <param name="RequestToDelivery">From the request to the hook handing it over: the session's turn ending.</param>
/// <param name="DeliveryToHandoff">From delivery to the handoff being written.</param>
/// <param name="RequestToHandoff">From the request to the handoff being written.</param>
/// <param name="HandoffToExit">From the handoff being written to /exit being typed.</param>
public sealed record StopDurations(
    TimeSpan Total,
    TimeSpan? RequestToDelivery,
    TimeSpan? DeliveryToHandoff,
    TimeSpan? RequestToHandoff,
    TimeSpan? HandoffToExit);
