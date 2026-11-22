namespace Theseus.Core.Links;

/// <summary>What installing, undoing, checking or registering the holdout guard came to, in words for the owner.</summary>
/// <param name="Succeeded">Whether it did what was asked.</param>
/// <param name="Message">What happened.</param>
public sealed record HoldoutGuardOutcome(bool Succeeded, string Message);
