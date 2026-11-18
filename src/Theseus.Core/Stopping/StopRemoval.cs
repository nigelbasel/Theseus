namespace Theseus.Core.Stopping;

/// <summary>Why a stop request was removed before its session was closed.</summary>
public enum StopRemoval
{
    /// <summary>Its card was started again.</summary>
    StartedAgain,

    /// <summary>The session running now started after the request, so it is not the one that was asked.</summary>
    NewerSession,

    /// <summary>Nigel declined the stops held back when the board opened.</summary>
    Declined,

    /// <summary>It was four hours old.</summary>
    Expired,
}
