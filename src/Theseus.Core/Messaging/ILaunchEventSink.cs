namespace Theseus.Core.Messaging;

/// <summary>
/// Where the application receives what the shims report. Every callback carries the ticket, because
/// a ticket is the only thing that ties a connection to the workstream that asked for it.
/// </summary>
public interface ILaunchEventSink
{
    /// <summary>A shim connected and redeemed a ticket.</summary>
    void ShimConnected(string ticket, int shimProcessId);

    /// <summary>A ticket was presented that could not be redeemed.</summary>
    void TicketRefused(string ticket, string reason);

    /// <summary>
    /// A ticket expired without any shim presenting it: the terminal took the launch but never ran
    /// the shim, so no session will start from it.
    /// </summary>
    void TicketLapsed(string ticket, string workstreamKey);

    /// <summary>Claude Code started in the shim's tab.</summary>
    void Started(string ticket, StartedPayload payload);

    /// <summary>A failed resume was retried under a freshly minted identifier.</summary>
    void Reminted(string ticket, RemintedPayload payload);

    /// <summary>Claude Code exited.</summary>
    void Exited(string ticket, ExitedPayload payload);

    /// <summary>The shim's connection dropped, which for a launched session is a death signal.</summary>
    void Disconnected(string ticket);
}
