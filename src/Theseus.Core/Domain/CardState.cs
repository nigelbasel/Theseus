namespace Theseus.Core.Domain;

/// <summary>What a card shows, and since when.</summary>
/// <param name="Lifecycle">The state itself.</param>
/// <param name="Since">When the card entered that state, where it is known.</param>
/// <param name="LastExitedUtc">For a card not started today, when its last run ended, where that was recorded.</param>
/// <param name="LastStartedUtc">For a card not started today, when its last run began.</param>
public sealed record CardState(
    SessionLifecycle Lifecycle,
    DateTimeOffset? Since,
    DateTimeOffset? LastExitedUtc = null,
    DateTimeOffset? LastStartedUtc = null);
