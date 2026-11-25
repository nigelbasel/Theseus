namespace Theseus.Core.Projects;

/// <summary>One action a stage offers, with its label and, when it is offered but cannot run, why not.</summary>
/// <param name="Action">The action.</param>
/// <param name="Label">What its button says.</param>
/// <param name="IsEnabled">Whether it can run now.</param>
/// <param name="DisabledReason">Why it cannot, shown as its tooltip; null when it can.</param>
public sealed record StageActionOffer(StageAction Action, string Label, bool IsEnabled = true, string? DisabledReason = null);
