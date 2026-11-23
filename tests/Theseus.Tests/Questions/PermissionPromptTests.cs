using Theseus.Core.Questions;

namespace Theseus.Tests.Questions;

/// <summary>When a recorded prompt is still what a session waits on: approving one fires no hook event (1.6 review).</summary>
[TestFixture]
public class PermissionPromptTests
{
    private const string SessionId = "11111111-2222-3333-4444-555555555555";
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);
    private static readonly PermissionPrompt Bash = new(SessionId, "Claude needs your permission to use Bash", Nine);

    [Test]
    public void APromptTheSessionWaitsAt_Shows()
    {
        Assert.That(PermissionPrompt.Showing(Bash, "waiting", null), Is.EqualTo(Bash));
    }

    [TestCase("busy")]
    [TestCase("idle")]
    [TestCase(null)]
    public void APromptTheSessionNoLongerWaitsAt_IsAnswered(string? status)
    {
        Assert.That(PermissionPrompt.Showing(Bash, status, null), Is.Null);
    }

    [Test]
    public void AQuestionAskedAfterAnApprovedPrompt_IsWhatShows()
    {
        var question = new OwnerQuestion(SessionId, "Which branch?", IsGuess: false, Nine.AddSeconds(10));

        Assert.That(PermissionPrompt.Showing(Bash, "waiting", question), Is.Null);
    }

    [Test]
    public void APromptMetAfterTheQuestion_IsWhatStopsTheSession()
    {
        var question = new OwnerQuestion(SessionId, "Which branch?", IsGuess: false, Nine.AddSeconds(-10));

        Assert.That(PermissionPrompt.Showing(Bash, "waiting", question), Is.EqualTo(Bash));
    }
}
