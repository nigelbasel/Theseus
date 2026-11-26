using System.Text.Json;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

/// <summary>Which turn endings are a transport error Theseus resumes from (2.4 W2), measured on this machine's transcripts.</summary>
[TestFixture]
public class TransportErrorTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 25, 7, 36, 57, TimeSpan.Zero);

    [TestCase("API Error: Can't reach the API server — check your internet or DNS (ENOTFOUND)")]
    [TestCase("API Error: Connection lost mid-response. The response above may be incomplete.")]
    [TestCase("API Error: Connection refused — a firewall or proxy may be blocking it (ConnectionRefused)")]
    [TestCase("API Error: 529 Overloaded. This is a server-side issue, usually temporary — try again in a moment.")]
    [TestCase("API Error: 500 Internal server error")]
    [TestCase("API Error: Request timed out.")]
    public void ANetworkOrServerError_IsATransportError(string text)
    {
        Assert.That(TransportError.EndedOn(Tail(Error(text))), Is.EqualTo(At));
    }

    [TestCase("Login expired · Please run /login")]
    [TestCase("API Error: 401 authentication_error · Please run /login")]
    [TestCase("You're out of usage credits. Run /usage-credits to keep using Fable 5.1 or /model to switch models.")]
    [TestCase("You've hit your session limit · resets 5:40pm (Europe/London)")]
    [TestCase("Claude Code can't use your Claude subscription, usually because a payment is past due")]
    [TestCase("API Error: 429 rate_limit_error")]
    [TestCase("API Error: 403 Forbidden")]
    public void ASignInOrALimit_IsNever(string text)
    {
        Assert.That(TransportError.EndedOn(Tail(Error(text))), Is.Null);
    }

    [Test]
    public void OnlyTheTurnsLastMessageCounts()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TransportError.EndedOn(Tail(Error("API Error: Connection lost mid-response."), Prompt("network is back"),
                Assistant("Carrying on."))), Is.Null, "it was resumed already");
            Assert.That(TransportError.EndedOn(Tail(Assistant("API Error: Connection lost mid-response."))), Is.Null,
                "a session quoting an error is not one");
            Assert.That(TransportError.EndedOn(Tail(Error("API Error: Connection lost mid-response."), System())), Is.EqualTo(At),
                "Claude Code's own records after it do not hide it");
            Assert.That(TransportError.EndedOn(null), Is.Null);
        });
    }

    [Test]
    public void Resumes_WaitLongerEachTime_UpToACap()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TransportResume.DueUtc(At, 0), Is.EqualTo(At.AddMinutes(1)));
            Assert.That(TransportResume.DueUtc(At, 1), Is.EqualTo(At.AddMinutes(2)));
            Assert.That(TransportResume.DueUtc(At, 3), Is.EqualTo(At.AddMinutes(8)));
            Assert.That(TransportResume.DueUtc(At, TransportResume.Cap - 1), Is.EqualTo(At.AddMinutes(32)));
            Assert.That(TransportResume.DueUtc(At, TransportResume.Cap), Is.Null, "after the cap it waits for the owner");
        });
    }

    [Test]
    public void AnErrorAlreadyTriedFor_IsNotTriedAgain_ButTheNextErrorIs_AfterALongerWait()
    {
        var later = At.AddMinutes(3);

        Assert.Multiple(() =>
        {
            Assert.That(TransportResume.NextTryUtc(At, null), Is.EqualTo(At.AddMinutes(1)), "the first error");
            Assert.That(TransportResume.NextTryUtc(At, (At, 1)), Is.Null, "its try has not ended on another error yet");
            Assert.That(TransportResume.NextTryUtc(later, (At, 1)), Is.EqualTo(later.AddMinutes(2)));
            Assert.That(TransportResume.NextTryUtc(later, (At, TransportResume.Cap)), Is.Null);
        });
    }

    [Test]
    public void TheResumeText_SaysCarryOnWhereYouWere()
    {
        Assert.That(TransportResume.Text, Does.StartWith("Carry on where you were"));
    }

    private static string Tail(params string[] lines) => string.Join('\n', lines);

    private static string Error(string text) => JsonSerializer.Serialize(new
    {
        type = "assistant",
        timestamp = At.ToString("O"),
        isApiErrorMessage = true,
        message = new { role = "assistant", model = "<synthetic>", content = new[] { new { type = "text", text } } },
    });

    private static string Assistant(string text) => JsonSerializer.Serialize(new
    {
        type = "assistant",
        timestamp = At.ToString("O"),
        message = new { role = "assistant", content = new[] { new { type = "text", text } } },
    });

    private static string Prompt(string text) =>
        JsonSerializer.Serialize(new { type = "user", timestamp = At.AddMinutes(30).ToString("O"), message = new { role = "user", content = text } });

    private static string System() => JsonSerializer.Serialize(new { type = "system", subtype = "turn_duration", timestamp = At.ToString("O") });
}
