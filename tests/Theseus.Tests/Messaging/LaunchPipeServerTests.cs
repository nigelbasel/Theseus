using System.Collections.Concurrent;
using Theseus.Core.Abstractions;
using Theseus.Core.Launching;
using Theseus.Core.Messaging;
using Theseus.Launcher;

namespace Theseus.Tests.Messaging;

[TestFixture]
public class LaunchPipeServerTests
{
    private const string SessionId = "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private string _pipeName = string.Empty;
    private RecordingSink _sink = null!;
    private FixedClock _clock = null!;
    private LaunchPipeServer _server = null!;

    [SetUp]
    public void SetUp()
    {
        _pipeName = $"Theseus.Tests.{Guid.NewGuid():N}";
        _sink = new RecordingSink();
        _clock = new FixedClock(new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero));
        _server = new LaunchPipeServer(_pipeName, _sink, _clock);
        _server.Start();
    }

    [TearDown]
    public async Task TearDown() => await _server.DisposeAsync();

    [Test]
    public void Redeem_HandsTheShimTheSpecificationItsTicketBuysWithNothingOnACommandLine()
    {
        _server.Register(Ticket("t-1"));

        using var client = Connect();
        var spec = client.Redeem("t-1", shimProcessId: 4242);

        Assert.That(spec, Is.Not.Null);
        Assert.That(spec!.SessionId, Is.EqualTo(SessionId));
        Assert.That(spec.Prompt, Is.EqualTo("Carry on; then stop."));
        Assert.That(spec.AddDirectories, Is.EqualTo(new[] { @"C:\dev\tfsgit\Core" }));
    }

    [Test]
    public void Redeem_TellsTheApplicationWhichShimProcessOwnsTheTab()
    {
        _server.Register(Ticket("t-1"));

        using var client = Connect();
        client.Redeem("t-1", shimProcessId: 4242);

        Assert.That(WaitFor(() => _sink.Connected.Count == 1), Is.True);
        Assert.That(_sink.Connected.Single(), Is.EqualTo(("t-1", 4242)));
    }

    [Test]
    public void Redeem_RefusesAnUnknownTicket()
    {
        using var client = Connect();

        Assert.That(client.Redeem("never-issued", 1), Is.Null);
        Assert.That(WaitFor(() => _sink.Refused.Count == 1), Is.True);
    }

    [Test]
    public void Redeem_RefusesATicketASecondTimeBecauseTicketsAreSingleUse()
    {
        _server.Register(Ticket("t-1"));

        using (var first = Connect())
        {
            Assert.That(first.Redeem("t-1", 1), Is.Not.Null);
        }

        using var second = Connect();
        Assert.That(second.Redeem("t-1", 2), Is.Null);
    }

    [Test]
    public void Redeem_RefusesAnExpiredTicket()
    {
        _server.Register(Ticket("t-1") with { ExpiresUtc = _clock.UtcNow.AddSeconds(-1) });

        using var client = Connect();

        Assert.That(client.Redeem("t-1", 1), Is.Null);
        Assert.That(WaitFor(() => _sink.Refused.Count == 1), Is.True);
        Assert.That(_sink.Refused.Single().Reason, Does.Contain("expired"));
    }

    [Test]
    public void Reports_ReachTheApplicationTaggedWithTheTicketTheyBelongTo()
    {
        _server.Register(Ticket("t-1"));

        using var client = Connect();
        client.Redeem("t-1", 4242);

        client.Started(SessionId, 8080);
        client.Reminted(SessionId, "minted-1", "Resume exited 1.");
        client.Exited("minted-1", 0, 1234);

        Assert.That(WaitFor(() => _sink.Exited.Count == 1), Is.True);
        Assert.That(_sink.Started.Single(), Is.EqualTo(("t-1", SessionId, 8080)));
        Assert.That(_sink.Reminted.Single(), Is.EqualTo(("t-1", SessionId, "minted-1")));
        Assert.That(_sink.Exited.Single(), Is.EqualTo(("t-1", "minted-1", 0, 1234L)));
    }

    [Test]
    public void Disconnect_IsReportedSoAClosedTabIsNoticedWithoutPolling()
    {
        _server.Register(Ticket("t-1"));

        using (var client = Connect())
        {
            client.Redeem("t-1", 4242);
        }

        Assert.That(WaitFor(() => _sink.Disconnected.Contains("t-1")), Is.True);
    }

    [Test]
    public void Server_ServesSeveralShimsAtOnce()
    {
        _server.Register(Ticket("t-1"));
        _server.Register(Ticket("t-2"));

        using var first = Connect();
        using var second = Connect();

        Assert.That(first.Redeem("t-1", 1), Is.Not.Null);
        Assert.That(second.Redeem("t-2", 2), Is.Not.Null);
    }

    private LauncherPipeClient Connect() => LauncherPipeClient.Connect(_pipeName, Patience);

    private static LaunchTicket Ticket(string ticket) =>
        new(ticket, "gdpr-mcp-contact-forms",
            new LaunchSpec(SessionId, LaunchMode.FirstRun, @"C:\claude.cmd", @"C:\dev\tfsgit",
                "GDPR MCP contact forms", "Carry on; then stop.", [@"C:\dev\tfsgit\Core"]),
            new DateTimeOffset(2026, 8, 26, 9, 2, 0, TimeSpan.Zero));

    private static bool WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Patience;

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return condition();
    }

    private sealed class FixedClock : ISystemClock
    {
        public FixedClock(DateTimeOffset now) => UtcNow = now;

        public DateTimeOffset UtcNow { get; }
    }

    private sealed class RecordingSink : ILaunchEventSink
    {
        public ConcurrentBag<(string Ticket, int ShimProcessId)> Connected { get; } = [];

        public ConcurrentBag<(string Ticket, string Reason)> Refused { get; } = [];

        public ConcurrentBag<(string Ticket, string SessionId, int ProcessId)> Started { get; } = [];

        public ConcurrentBag<(string Ticket, string Previous, string Minted)> Reminted { get; } = [];

        public ConcurrentBag<(string Ticket, string SessionId, int ExitCode, long DurationMs)> Exited { get; } = [];

        public ConcurrentBag<string> Disconnected { get; } = [];

        public void ShimConnected(string ticket, int shimProcessId) => Connected.Add((ticket, shimProcessId));

        public void TicketRefused(string ticket, string reason) => Refused.Add((ticket, reason));

        void ILaunchEventSink.Started(string ticket, StartedPayload payload) =>
            Started.Add((ticket, payload.SessionId, payload.ClaudeProcessId));

        void ILaunchEventSink.Reminted(string ticket, RemintedPayload payload) =>
            Reminted.Add((ticket, payload.PreviousSessionId, payload.NewSessionId));

        void ILaunchEventSink.Exited(string ticket, ExitedPayload payload) =>
            Exited.Add((ticket, payload.SessionId, payload.ExitCode, payload.DurationMs));

        void ILaunchEventSink.Disconnected(string ticket) => Disconnected.Add(ticket);
    }
}
