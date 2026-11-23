namespace Theseus.Core.Launching;

/// <summary>
/// The launches whose runs have not ended, by card, so one card started twice never opens a second session (git-22,
/// 2026-11-23). A launch stays in flight from its ticket until its run ends: while its tab waits at Claude Code's question
/// about trusting the folder no session is registered, so the board's own check sees nothing running. Told of endings on
/// the launch pipe's threads, so it is locked.
/// </summary>
public sealed class LaunchesInFlight
{
    private readonly Dictionary<string, string> _ticketToCard = new(StringComparer.Ordinal);
    private readonly Lock _lock = new();

    /// <summary>A card's launch was issued a ticket.</summary>
    public void Began(string card, string ticket)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(card);
        ArgumentException.ThrowIfNullOrWhiteSpace(ticket);

        lock (_lock)
        {
            _ticketToCard[ticket] = card;
        }
    }

    /// <summary>A ticket's run ended however it ended: exited, disconnected, refused, or never redeemed.</summary>
    public void Ended(string ticket)
    {
        lock (_lock)
        {
            _ticketToCard.Remove(ticket);
        }
    }

    /// <summary>Whether a card has a launch whose run has not ended.</summary>
    public bool IsInFlight(string card)
    {
        lock (_lock)
        {
            return _ticketToCard.Values.Any(each => string.Equals(each, card, StringComparison.OrdinalIgnoreCase));
        }
    }
}
