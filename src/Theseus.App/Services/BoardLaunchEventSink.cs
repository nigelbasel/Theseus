using System.Windows.Threading;
using Theseus.App.ViewModels;
using Theseus.Core.Abstractions;
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
    private readonly IRunLog _runs;
    private readonly Dictionary<string, Launched> _ticketsToWorkstreams = new(StringComparer.Ordinal);

    /// <summary>Creates the sink over the board it updates and the log it records endings in.</summary>
    public BoardLaunchEventSink(BoardViewModel board, Dispatcher dispatcher, IRunLog runs)
    {
        _board = board;
        _dispatcher = dispatcher;
        _runs = runs;
    }

    /// <summary>Remembers which workstream and run a ticket belongs to, before the shim redeems it.</summary>
    public void Expect(string ticket, string workstreamKey, Guid runId)
    {
        lock (_ticketsToWorkstreams)
        {
            _ticketsToWorkstreams[ticket] = new Launched(workstreamKey, runId);
        }
    }

    /// <inheritdoc />
    public void ShimConnected(string ticket, int shimProcessId) => Set(ticket, SessionLifecycle.Launching);

    /// <inheritdoc />
    public void TicketRefused(string ticket, string reason) => Set(ticket, SessionLifecycle.Ghost);

    /// <inheritdoc />
    public void Started(string ticket, StartedPayload payload) => Set(ticket, SessionLifecycle.Running);

    /// <summary>Told, in words, when a start never reached its tab, so Nigel knows to start it again.</summary>
    public Action<string>? StartLost { get; set; }

    /// <inheritdoc />
    public void TicketLapsed(string ticket, string workstreamKey)
    {
        End(ticket, null, SessionExitReason.NeverStarted);
        Set(ticket, SessionLifecycle.Ghost);

        lock (_ticketsToWorkstreams)
        {
            _ticketsToWorkstreams.Remove(ticket);
        }

        _dispatcher.BeginInvoke(() =>
        {
            var title = _board.Card(workstreamKey)?.Title ?? workstreamKey;

            StartLost?.Invoke($"{title} did not start: Windows Terminal opened the tab but never ran the session in it."
                              + " Close that tab and press Start again.");
        });
    }

    /// <inheritdoc />
    public void Reminted(string ticket, RemintedPayload payload) =>
        Post(ticket, card => _board.RememberSession(card.Key, payload.NewSessionId));

    /// <inheritdoc />
    public void Exited(string ticket, ExitedPayload payload)
    {
        End(ticket, payload.ExitCode,
            payload.ExitCode == 0 ? SessionExitReason.ClosedCleanly : SessionExitReason.CrashedOrKilled);

        Set(ticket, payload.ExitCode == 0 ? SessionLifecycle.Ended : SessionLifecycle.Ghost);
    }

    /// <summary>
    /// The shim has gone. Where it never reported an exit - a closed tab, a killed process - this is
    /// the last chance to record that the run is over, so an ending is written with no exit code.
    /// </summary>
    public void Disconnected(string ticket)
    {
        End(ticket, null, SessionExitReason.CrashedOrKilled);

        lock (_ticketsToWorkstreams)
        {
            _ticketsToWorkstreams.Remove(ticket);
        }
    }

    /// <summary>
    /// Records an ending once. The flag spares the log a second line; it is not what makes this
    /// correct, which is that the fold on read keeps the first ending a run is given.
    /// </summary>
    private void End(string ticket, int? exitCode, SessionExitReason reason)
    {
        Launched launched;

        lock (_ticketsToWorkstreams)
        {
            if (!_ticketsToWorkstreams.TryGetValue(ticket, out var found) || found.Ended)
            {
                return;
            }

            launched = found;
            _ticketsToWorkstreams[ticket] = found with { Ended = true };
        }

        _runs.AppendEnd(new RunEnd(launched.RunId, DateTimeOffset.UtcNow, exitCode, reason));
    }

    private void Set(string ticket, SessionLifecycle lifecycle) =>
        Post(ticket, card =>
        {
            card.Lifecycle = lifecycle;
            card.LifecycleSinceUtc = DateTimeOffset.UtcNow;
        });

    private void Post(string ticket, Action<WorkstreamCardViewModel> action)
    {
        Launched? launched;

        lock (_ticketsToWorkstreams)
        {
            launched = _ticketsToWorkstreams.TryGetValue(ticket, out var found) ? found : null;
        }

        if (launched is not { WorkstreamKey: var key })
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
