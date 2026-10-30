using Theseus.Core.Domain;
using Theseus.Core.Launching;

namespace Theseus.Tests.Launching;

/// <summary>Starting a paused or blocked workstream asks first whether the mark still holds.</summary>
[TestFixture]
public class FlaggedStartTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 30, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void Among_FindsOnlyTheMarkedOnes()
    {
        var marked = FlaggedStart.Among([Card("plain"), Card("paused", paused: true), Card("blocked", blocked: true)]);

        Assert.That(marked.Select(workstream => workstream.Key), Is.EqualTo(new[] { "paused", "blocked" }));
    }

    [Test]
    public void Question_NamesEachMarkAndWhatNoDoesWhenOthersStartToo()
    {
        var question = FlaggedStart.Question([Card("auth", paused: true, blocked: true)], others: 2);

        Assert.Multiple(() =>
        {
            Assert.That(question, Does.Contain("Auth is marked paused and blocked."));
            Assert.That(question, Does.Contain("Is it no longer paused or blocked?"));
            Assert.That(question, Does.Contain("No leaves it marked and starts only the other 2."));
        });
    }

    [Test]
    public void Question_WhenNothingElseIsStarting()
    {
        var question = FlaggedStart.Question([Card("auth", paused: true), Card("cache", blocked: true)], others: 0);

        Assert.That(question, Does.Contain("Cache is marked blocked."));
        Assert.That(question, Does.Contain("No leaves them marked and starts nothing."));
    }

    [Test]
    public void Cleared_TakesBothMarksOff()
    {
        var cleared = FlaggedStart.Cleared(Card("auth", paused: true, blocked: true));

        Assert.That((cleared.IsPaused, cleared.IsBlocked), Is.EqualTo((false, false)));
    }

    private static Workstream Card(string key, bool paused = false, bool blocked = false) =>
        new(key, char.ToUpperInvariant(key[0]) + key[1..], "#208DD7", false, @"C:\work", [], "Carry on.", 0, "New",
            blocked, false, false, null, [], null, Nine)
        {
            IsPaused = paused,
        };
}
