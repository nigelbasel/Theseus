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

    /// <summary>Every request still waiting.</summary>
    IReadOnlyList<PendingStopRequest> Pending();
}
