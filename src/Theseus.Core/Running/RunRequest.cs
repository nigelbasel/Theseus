namespace Theseus.Core.Running;

/// <summary>
/// A request to run a card. The runner, not the caller, picks the card's mode and checks its budgets and breakers, so a
/// duty or the queue never starts a session itself (agreed with the duties workstream, 2026-11-24).
/// </summary>
/// <param name="Workstream">The card's key.</param>
/// <param name="Reason">What asked for the run.</param>
/// <param name="RequestedBy">Who asked, as shown on the card: the owner, or the duty's or card's name.</param>
/// <param name="Prompt">An instruction for this run, given last in its opening; null for none.</param>
public sealed record RunRequest(string Workstream, RunReason Reason, string RequestedBy, string? Prompt = null);
