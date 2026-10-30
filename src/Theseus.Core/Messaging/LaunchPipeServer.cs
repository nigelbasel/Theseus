using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text;
using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Messaging;

/// <summary>
/// The application's end of the launch pipe. It hands each shim the specification its ticket
/// redeems, then listens until the shim's tab dies. Nothing here is the system of record: if this
/// server is not running, sessions still launch and are still discovered through the registry.
/// </summary>
public sealed class LaunchPipeServer : IAsyncDisposable
{
    private readonly string _pipeName;
    private readonly ILaunchEventSink _sink;
    private readonly ISystemClock _clock;
    private readonly ConcurrentDictionary<string, LaunchTicket> _tickets = new(StringComparer.Ordinal);

    /// <summary>
    /// Tickets this server has handed out, and to which shim. Kept so a second shim presenting the
    /// same ticket can be told apart from one presenting a ticket this Theseus never issued — the
    /// refusal message cannot tell them apart, and they mean opposite things.
    /// </summary>
    private readonly ConcurrentDictionary<string, (int ShimProcessId, DateTimeOffset WhenUtc)> _redeemed =
        new(StringComparer.Ordinal);

    /// <summary>When this server started, so a ticket it never issued can be dated against it.</summary>
    private readonly DateTimeOffset _startedUtc = DateTimeOffset.UtcNow;
    private readonly CancellationTokenSource _stopping = new();
    private readonly UTF8Encoding _encoding = new(encoderShouldEmitUTF8Identifier: false);

    private Task? _listener;
    private Task? _sweeper;

    /// <summary>How often tickets are checked for having lapsed unredeemed.</summary>
    public static readonly TimeSpan SweepInterval = TimeSpan.FromSeconds(15);

    /// <summary>Creates a server that has not yet started listening.</summary>
    public LaunchPipeServer(string pipeName, ILaunchEventSink sink, ISystemClock clock)
    {
        _pipeName = pipeName;
        _sink = sink;
        _clock = clock;
    }

    /// <summary>Makes a ticket redeemable until it expires or is used, whichever comes first.</summary>
    public void Register(LaunchTicket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        _tickets[ticket.Ticket] = ticket;

        TheseusLog.Debug("Ticket {Ticket} registered for {Workstream}; {Outstanding} now outstanding",
            ticket.Ticket, ticket.WorkstreamKey, _tickets.Count);
    }

    /// <summary>
    /// Takes back a ticket whose terminal would not start, so it is not swept two minutes later as a
    /// launch the terminal took and never ran. The ticket is still registered before the terminal is
    /// started, because a fast launcher must not present one that is not yet known.
    /// </summary>
    public void Withdraw(string ticket)
    {
        if (_tickets.TryRemove(ticket, out var withdrawn))
        {
            TheseusLog.Debug("Ticket {Ticket} for {Workstream} withdrawn: its terminal did not start", ticket, withdrawn.WorkstreamKey);
        }
    }

    /// <summary>Begins accepting shim connections, and watching for tickets nobody redeems.</summary>
    public void Start()
    {
        _listener ??= Task.Run(() => ListenAsync(_stopping.Token));
        _sweeper ??= Task.Run(() => SweepAsync(_stopping.Token));
    }

    /// <summary>
    /// Drops every ticket that expired without a shim presenting it, and reports each one. On
    /// 2026-10-23 Windows Terminal opened a plain cmd tab for a launch instead of running the shim,
    /// and nothing anywhere said so: the ticket just sat here until the next restart.
    /// </summary>
    /// <returns>How many tickets lapsed.</returns>
    public int SweepLapsed()
    {
        var now = _clock.UtcNow;
        var lapsed = 0;

        foreach (var (key, ticket) in _tickets)
        {
            if (ticket.ExpiresUtc > now || !_tickets.TryRemove(key, out _))
            {
                continue;
            }

            lapsed++;
            TheseusLog.Warning(
                "Ticket {Ticket} for {Workstream} lapsed at {Expires:HH:mm:ss} with no launcher presenting it: the terminal took the launch but never ran it",
                ticket.Ticket, ticket.WorkstreamKey, ticket.ExpiresUtc.ToLocalTime());
            _sink.TicketLapsed(ticket.Ticket, ticket.WorkstreamKey);
        }

        return lapsed;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stopping.CancelAsync();

        if (_listener is not null)
        {
            try
            {
                await _listener;
            }
            catch (OperationCanceledException)
            {
            }
        }

        if (_sweeper is not null)
        {
            try
            {
                await _sweeper;
            }
            catch (OperationCanceledException)
            {
            }
        }

        _stopping.Dispose();
    }

    private async Task SweepAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(SweepInterval, cancellationToken);
            SweepLapsed();
        }
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var server = new NamedPipeServerStream(_pipeName, PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances, PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            try
            {
                await server.WaitForConnectionAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                await server.DisposeAsync();
                return;
            }

            _ = Task.Run(() => ServeAsync(server, cancellationToken), CancellationToken.None);
        }
    }

    private async Task ServeAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        var ticket = string.Empty;

        try
        {
            using var reader = new StreamReader(server, _encoding, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
            await using var writer = new StreamWriter(server, _encoding, leaveOpen: true) { AutoFlush = true };

            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (!ShimProtocol.TryParse(line, out var envelope))
                {
                    continue;
                }

                switch (envelope.Kind)
                {
                    case ShimMessageKind.Hello:
                        ticket = await HandleHelloAsync(envelope, writer, ticket);
                        break;
                    case ShimMessageKind.Started when ShimProtocol.PayloadAs<StartedPayload>(envelope) is { } started:
                        _sink.Started(ticket, started);
                        break;
                    case ShimMessageKind.Reminted when ShimProtocol.PayloadAs<RemintedPayload>(envelope) is { } reminted:
                        _sink.Reminted(ticket, reminted);
                        break;
                    case ShimMessageKind.Exited when ShimProtocol.PayloadAs<ExitedPayload>(envelope) is { } exited:
                        _sink.Exited(ticket, exited);
                        break;
                }
            }
        }
        catch (IOException exception)
        {
            TheseusLog.Debug("Shim connection for ticket {Ticket} dropped: {Reason}", ticket, exception.Message);
        }
        catch (OperationCanceledException)
        {
            TheseusLog.Debug("Stopped serving ticket {Ticket} because Theseus is closing", ticket);
        }
        finally
        {
            await server.DisposeAsync();
            _sink.Disconnected(ticket);
        }
    }

    private async Task<string> HandleHelloAsync(ShimEnvelope envelope, StreamWriter writer, string currentTicket)
    {
        var hello = ShimProtocol.PayloadAs<HelloPayload>(envelope);
        if (hello is null)
        {
            await RefuseAsync(writer, currentTicket, "The hello message could not be read.");
            return currentTicket;
        }

        if (!_tickets.TryRemove(hello.Ticket, out var ticket))
        {
            // "Unknown or already used" is two very different faults wearing one message, and on
            // 2026-09-08 that cost an investigation: a tab said it, and nothing anywhere recorded
            // which of the two had happened, who else had redeemed the ticket, or when. A ticket
            // this server issued and saw redeemed is a duplicate shim; one it never issued belongs
            // to a Theseus that has since been replaced.
            var seen = _redeemed.TryGetValue(hello.Ticket, out var already);

            TheseusLog.Warning(
                seen
                    ? "Shim {Shim} presented ticket {Ticket}, which shim {First} already redeemed at {When:HH:mm:ss}"
                    : "Shim {Shim} presented ticket {Ticket}, which this Theseus never issued (first started {When:HH:mm:ss}); {Outstanding} outstanding",
                hello.ShimProcessId,
                hello.Ticket,
                seen ? already.ShimProcessId : _startedUtc,
                seen ? already.WhenUtc : _startedUtc,
                _tickets.Count);

            await RefuseAsync(writer, hello.Ticket, "That launch ticket is unknown or has already been used.");
            return hello.Ticket;
        }

        _redeemed[hello.Ticket] = (hello.ShimProcessId, _clock.UtcNow);

        if (ticket.ExpiresUtc <= _clock.UtcNow)
        {
            await RefuseAsync(writer, hello.Ticket, "That launch ticket has expired.");
            return hello.Ticket;
        }

        _sink.ShimConnected(hello.Ticket, hello.ShimProcessId);
        await writer.WriteLineAsync(ShimProtocol.Serialise(ShimMessageKind.LaunchSpec, ticket.Spec));

        return hello.Ticket;
    }

    private async Task RefuseAsync(StreamWriter writer, string ticket, string reason)
    {
        _sink.TicketRefused(ticket, reason);
        await writer.WriteLineAsync(ShimProtocol.Serialise(ShimMessageKind.Error, new ErrorPayload(reason)));
    }
}
