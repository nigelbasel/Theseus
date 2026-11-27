namespace Theseus.Core.Guards;

/// <summary>What installing, undoing, checking or registering a guard came to, in words for the owner.</summary>
/// <param name="Succeeded">Whether it did what was asked.</param>
/// <param name="Message">What happened.</param>
public sealed record GuardOutcome(bool Succeeded, string Message);
