using Theseus.Core.Attention;
using Theseus.Core.Domain;

namespace Theseus.Tests.Attention;

[TestFixture]
public class SessionCardsTests
{
    [Test]
    public void Current_ReadsTheRunLogAgainOnlyWhenItsLengthChanges()
    {
        var reads = 0;
        long length = 10;
        IReadOnlyList<SessionRun> runs = [Run("s1", "alpha")];
        var cards = new SessionCards(() =>
        {
            reads++;

            return runs;
        }, () => length);

        cards.Current();
        cards.Current();
        runs = [Run("s1", "alpha"), Run("s2", "beta")];
        length = 20;

        Assert.That(cards.Current()["s2"], Is.EqualTo("beta"));
        Assert.That(reads, Is.EqualTo(2));
    }

    private static SessionRun Run(string session, string card) =>
        new(Guid.NewGuid(), session, card, @"C:\work", null, SessionLaunchMode.FirstRun, "Prompt.", card, "#208DD7", DateTimeOffset.UtcNow,
            null, null, null, null, SessionExitReason.Unknown);
}
