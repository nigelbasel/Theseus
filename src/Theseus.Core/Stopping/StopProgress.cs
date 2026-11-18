namespace Theseus.Core.Stopping;

/// <summary>
/// How far a stop got before its request was removed without the session being closed. Nearly half the stops
/// on one machine between 2026-11-03 and 2026-11-11 ended this way with nothing saying where they stuck
/// (stop-timing research).
/// </summary>
public enum StopProgress
{
    /// <summary>The request was still waiting: the session never ended a turn after it was asked.</summary>
    NeverDelivered,

    /// <summary>The session was told, but wrote no handoff afterwards.</summary>
    DeliveredNoHandoff,

    /// <summary>The session handed over, but was never closed.</summary>
    HandedOverNotClosed,
}
