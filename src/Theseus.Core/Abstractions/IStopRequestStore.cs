using Theseus.Core.Stopping;

namespace Theseus.Core.Abstractions;

/// <summary>Where stop requests wait between the board asking and a session being told.</summary>
public interface IStopRequestStore
{
    /// <summary>Records a request for one session, replacing any it has not yet been given.</summary>
    void Write(PendingStopRequest request);

    /// <summary>Withdraws an undelivered request. A request already given cannot be taken back.</summary>
    bool Cancel(string sessionId);

    /// <summary>
    /// Takes the request for one session, if there is one, so that nothing else can take it too.
    /// </summary>
    PendingStopRequest? Claim(string sessionId);

    /// <summary>The request waiting for one session, without taking it.</summary>
    PendingStopRequest? Waiting(string sessionId);

    /// <summary>Every request still waiting.</summary>
    IReadOnlyList<PendingStopRequest> Pending();

    /// <summary>Discards requests too old to be worth delivering, and says how many went.</summary>
    int Sweep(DateTimeOffset now);

    /// <summary>The request this session was handed, if it has been handed one.</summary>
    PendingStopRequest? Delivered(string? sessionId);

    /// <summary>
    /// Finishes with a delivered request, once the session it named has been told to exit.
    ///
    /// A request that is not retired is acted on again by the next Theseus to start, because the
    /// only thing stopping a second <c>/exit</c> was a set held in memory that dies with the process.
    /// On 2026-09-02 that typed <c>/exit</c> into sessions hours after they were asked to stop, and
    /// into ones that had since been resumed - closing the same window twice.
    /// </summary>
    bool Retire(string? sessionId);
}
