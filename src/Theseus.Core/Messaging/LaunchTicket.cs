using Theseus.Core.Launching;

namespace Theseus.Core.Messaging;

/// <summary>
/// A single-use, expiring claim on a launch specification. The ticket is the only thing that travels
/// on the shim's command line, so it must be worthless to anyone who is not the shim it was minted for.
/// </summary>
/// <param name="Ticket">The identifier handed to the shim.</param>
/// <param name="WorkstreamKey">The workstream this launch belongs to.</param>
/// <param name="Spec">What the ticket redeems.</param>
/// <param name="ExpiresUtc">When the ticket stops being redeemable.</param>
public sealed record LaunchTicket(
    string Ticket,
    string WorkstreamKey,
    LaunchSpec Spec,
    DateTimeOffset ExpiresUtc);
