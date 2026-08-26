namespace Theseus.Core.Domain;

/// <summary>What a card shows, and since when.</summary>
/// <param name="Lifecycle">The state itself.</param>
/// <param name="Since">When the card entered that state, where it is known.</param>
public sealed record CardState(SessionLifecycle Lifecycle, DateTimeOffset? Since);
