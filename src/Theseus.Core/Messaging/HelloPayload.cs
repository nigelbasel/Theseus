namespace Theseus.Core.Messaging;

/// <summary>Shim to app: the ticket being redeemed and the shim's process id.</summary>
/// <param name="Ticket">The single-use launch ticket.</param>
/// <param name="ShimProcessId">The shim's own process id, whose parent is the terminal.</param>
public sealed record HelloPayload(string Ticket, int ShimProcessId);
