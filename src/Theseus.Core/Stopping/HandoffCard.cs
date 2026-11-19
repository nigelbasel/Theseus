namespace Theseus.Core.Stopping;

/// <summary>What deciding a card's handover file needs to know about the card.</summary>
/// <param name="Key">The card's key.</param>
/// <param name="Title">The card's title.</param>
/// <param name="File">The handover file it resolves to.</param>
/// <param name="IsChosen">Whether the card names that file itself, rather than finding it by search.</param>
/// <param name="IsBusy">Whether a session of it is running or being started (<see cref="HandoffBusy"/>).</param>
public sealed record HandoffCard(string Key, string Title, string File, bool IsChosen, bool IsBusy);
