using Theseus.Core.Launching;
using Theseus.Core.Messaging;

namespace Theseus.Tests.Messaging;

[TestFixture]
public class ShimProtocolTests
{
    private const string SessionId = "3f2b1c40-9d5e-4a10-8b77-2c9e1d4a55aa";

    [Test]
    public void Serialise_ProducesASingleLine()
    {
        var line = ShimProtocol.Serialise(ShimMessageKind.Hello, new HelloPayload("ticket-1", 4242));

        Assert.That(line, Does.Not.Contain("\n"));
    }

    [Test]
    public void RoundTrip_CarriesHello()
    {
        var payload = RoundTrip<HelloPayload>(ShimMessageKind.Hello, new HelloPayload("ticket-1", 4242));

        Assert.That(payload!.Ticket, Is.EqualTo("ticket-1"));
        Assert.That(payload.ShimProcessId, Is.EqualTo(4242));
    }

    [Test]
    public void RoundTrip_CarriesStarted()
    {
        var payload = RoundTrip<StartedPayload>(ShimMessageKind.Started, new StartedPayload(SessionId, 99));

        Assert.That(payload!.SessionId, Is.EqualTo(SessionId));
        Assert.That(payload.ClaudeProcessId, Is.EqualTo(99));
    }

    [Test]
    public void RoundTrip_CarriesReminted()
    {
        var payload = RoundTrip<RemintedPayload>(ShimMessageKind.Reminted,
            new RemintedPayload(SessionId, "new-id", "Resume exited 1."));

        Assert.That(payload!.PreviousSessionId, Is.EqualTo(SessionId));
        Assert.That(payload.NewSessionId, Is.EqualTo("new-id"));
        Assert.That(payload.Reason, Is.EqualTo("Resume exited 1."));
    }

    [Test]
    public void RoundTrip_CarriesExited()
    {
        var payload = RoundTrip<ExitedPayload>(ShimMessageKind.Exited, new ExitedPayload(SessionId, 3, 1234));

        Assert.That(payload!.ExitCode, Is.EqualTo(3));
        Assert.That(payload.DurationMs, Is.EqualTo(1234));
    }

    [Test]
    public void RoundTrip_CarriesError()
    {
        var payload = RoundTrip<ErrorPayload>(ShimMessageKind.Error, new ErrorPayload("Ticket expired."));

        Assert.That(payload!.Message, Is.EqualTo("Ticket expired."));
    }

    [Test]
    public void RoundTrip_CarriesALaunchSpecIncludingItsModeAndDirectories()
    {
        var spec = new LaunchSpec(SessionId, LaunchMode.Resume, @"C:\claude.cmd", @"C:\work\repo",
            "Contact form validation", "Carry on; then stop.", [@"C:\work\repo\Core"]);

        var payload = RoundTrip<LaunchSpec>(ShimMessageKind.LaunchSpec, spec);

        Assert.That(payload!.Mode, Is.EqualTo(LaunchMode.Resume));
        Assert.That(payload.Prompt, Is.EqualTo("Carry on; then stop."));
        Assert.That(payload.AddDirectories, Is.EqualTo(new[] { @"C:\work\repo\Core" }));
    }

    [Test]
    public void RoundTrip_CarriesTheOpeningARemintWouldNeed()
    {
        var spec = new LaunchSpec(SessionId, LaunchMode.Resume, @"C:\claude.cmd", @"C:\work\repo",
            "Contact form validation", "Resumed at 08:02.", []) { PromptIfReminted = "Read the handoff." };

        var payload = RoundTrip<LaunchSpec>(ShimMessageKind.LaunchSpec, spec);

        Assert.That(payload!.PromptIfReminted, Is.EqualTo("Read the handoff."));
    }

    [Test]
    public void TryParse_RejectsAKindThisBuildDoesNotKnowRatherThanThrowing()
    {
        var understood = ShimProtocol.TryParse("""{"kind":"Teleported","payload":{}}""", out _);

        Assert.That(understood, Is.False);
    }

    [Test]
    public void TryParse_RejectsMalformedJsonRatherThanThrowing()
    {
        Assert.That(ShimProtocol.TryParse("{not json", out _), Is.False);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void TryParse_RejectsBlankLines(string? line)
    {
        Assert.That(ShimProtocol.TryParse(line, out _), Is.False);
    }

    [Test]
    public void PayloadAs_ReturnsNullWhenThePayloadDoesNotFitTheRequestedShape()
    {
        Assert.That(ShimProtocol.TryParse("""{"kind":"Hello","payload":"not an object"}""", out var envelope), Is.True);
        Assert.That(ShimProtocol.PayloadAs<HelloPayload>(envelope), Is.Null);
    }

    private static TPayload? RoundTrip<TPayload>(ShimMessageKind kind, TPayload payload)
        where TPayload : class
    {
        var line = ShimProtocol.Serialise(kind, payload);

        Assert.That(ShimProtocol.TryParse(line, out var envelope), Is.True);
        Assert.That(envelope.Kind, Is.EqualTo(kind));

        return ShimProtocol.PayloadAs<TPayload>(envelope);
    }
}
