using Theseus.Core.Questions;

namespace Theseus.Tests.Questions;

/// <summary>A tab stopped at Claude Code's folder trust question waits for the owner, though no session is registered yet.</summary>
[TestFixture]
public class TrustPromptTests
{
    private const string SessionId = "11111111-2222-3333-4444-555555555555";
    private static readonly DateTimeOffset Nine = new(2026, 11, 23, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void ALaunchIntoAnUntrustedFolder_NotYetRegistered_WaitsForTheOwner()
    {
        var prompt = TrustPrompt.For(SessionId, isRegistered: false, launchInFlight: true, ["E:/work/new/Repo"], Nine);

        Assert.That(prompt, Is.Not.Null);
        Assert.That(QuestionWords.Card(prompt!), Is.EqualTo("Needs you to trust E:/work/new/Repo before it starts"));
        Assert.That(QuestionWords.Named("W7", prompt!), Is.EqualTo("W7 needs you to trust E:/work/new/Repo before it starts"));
    }

    [TestCase(true, true, "the session registered, so the question was answered")]
    [TestCase(false, false, "no launch in flight: nothing is at the question")]
    public void OtherwiseNothingWaits(bool isRegistered, bool launchInFlight, string why)
    {
        Assert.That(TrustPrompt.For(SessionId, isRegistered, launchInFlight, ["E:/work/new/Repo"], Nine), Is.Null, why);
    }

    [Test]
    public void TrustedFolders_AreNotAsked()
    {
        Assert.That(TrustPrompt.For(SessionId, isRegistered: false, launchInFlight: true, [], Nine), Is.Null);
    }
}
