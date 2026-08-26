namespace Theseus.Core.Messaging;

/// <summary>Shim to app: a resume failed immediately and was retried under a new identifier.</summary>
/// <param name="PreviousSessionId">The identifier the failed resume used.</param>
/// <param name="NewSessionId">The freshly minted identifier.</param>
/// <param name="Reason">Why the resume was abandoned.</param>
public sealed record RemintedPayload(string PreviousSessionId, string NewSessionId, string Reason);
