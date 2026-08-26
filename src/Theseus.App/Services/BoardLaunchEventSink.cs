using System.Windows.Threading;
using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Messaging;

namespace Theseus.App.Services;

/// <summary>
/// Turns what the launcher shims report into card state. Reports arrive on pipe threads, so every
/// one is marshalled onto the dispatcher before it touches a view model.
/// </summary>
public sealed class BoardLaunchEventSink : ILaunchEventSink
{
    private readonly BoardViewModel _board;
    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<string, string> _ticketsToWorkstreams = new(StringComparer.Ordinal);

    /// <summary>Creates the sink over the board it updates.</summary>
    public BoardLaunchEventSink(BoardViewModel board, Dispatcher dispatcher)
    {
        _board = board;
        _dispatcher = dispatcher;
    }

    /// <summary>Remembers which workstream a ticket belongs to, before the shim redeems it.</summary>
    public void Expect(string ticket, string workstreamKey)
    {
        lock (_ticketsToWorkstreams)
        {
            _ticketsToWorkstreams[ticket] = workstreamKey;
        }
    }

    /// <inheritdoc />
    public void ShimConnected(string ticket, int shimProcessId) => Set(ticket, SessionLifecycle.Launching);

    /// <inheritdoc />
    public void TicketRefused(string ticket, string reason) => Set(ticket, SessionLifecycle.Ghost);

    /// <inheritdoc />
    public void Started(string ticket, StartedPayload payload) => Set(ticket, SessionLifecycle.Running);

    /// <inheritdoc />
    public void Reminted(string ticket, RemintedPayload payload) =>
        Post(ticket, card => _board.RememberSession(card.Key, payload.NewSessionId));

    /// <inheritdoc />
    public void Exited(string ticket, ExitedPayload payload) =>
        Set(ticket, payload.ExitCode == 0 ? SessionLifecycle.Ended : SessionLifecycle.Ghost);

    /// <inheritdoc />
    public void Disconnected(string ticket)
    {
        lock (_ticketsToWorkstreams)
        {
            _ticketsToWorkstreams.Remove(ticket);
        }
    }

    private void Set(string ticket, SessionLifecycle lifecycle) =>
        Post(ticket, card =>
        {
            card.Lifecycle = lifecycle;
            card.LifecycleSinceUtc = DateTimeOffset.UtcNow;
        });

    private void Post(string ticket, Action<WorkstreamCardViewModel> action)
    {
        string? key;

        lock (_ticketsToWorkstreams)
        {
            _ticketsToWorkstreams.TryGetValue(ticket, out key);
        }

        if (key is null)
        {
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            if (_board.Card(key) is { } card)
            {
                action(card);
            }
        });
    }
}
