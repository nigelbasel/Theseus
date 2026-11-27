using Theseus.Core.Domain;
using Theseus.Core.Locks;

namespace Theseus.App.ViewModels;

/// <summary>
/// Whether a session hold should show amber on the locks panel, for Nigel to release: its session has been idle on the
/// board for <see cref="Threshold"/> or has gone. Nothing releases such a hold by itself.
/// </summary>
public static class LockIdle
{
    public static readonly TimeSpan Threshold = TimeSpan.FromMinutes(30);

    public static string? For(LockTicket ticket, IEnumerable<WorkstreamCardViewModel> cards, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(cards);

        if (ticket is not { Mode: HoldMode.Session, IsHolder: true, SessionId: { } sessionId })
        {
            return null;
        }

        WorkstreamCardViewModel? card = cards.FirstOrDefault(candidate =>
            string.Equals(candidate.Workstream.LastSessionId, sessionId, StringComparison.OrdinalIgnoreCase));

        if (card is null || !card.IsLive)
        {
            return "its session has gone";
        }

        return card is { Lifecycle: SessionLifecycle.Idle, LifecycleSinceUtc: { } since } && now - since >= Threshold
            ? $"session idle {LocksViewModel.Say(now - since)}"
            : null;
    }
}
