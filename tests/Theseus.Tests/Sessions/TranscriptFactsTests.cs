using Theseus.App.Services;

namespace Theseus.Tests.Sessions;

/// <summary>
/// The board's transcript reads happen on a worker and are shown once known, so a refresh never
/// waits on a disk read.
/// </summary>
[TestFixture]
public class TranscriptFactsTests
{
    [Test]
    public void Known_IsEmptyUntilTheTranscriptHasBeenRead()
    {
        var facts = new TranscriptFacts((_, _) => new TranscriptFact(new TranscriptTail.Measurement(10, 20), "Opus 5.5"));

        Assert.That(facts.Known("a"), Is.EqualTo(default(TranscriptFact)));
    }

    [Test]
    public async Task Refresh_ReadsInTheBackground_AndSaysWhenSomethingChanged()
    {
        var gate = new ManualResetEventSlim();
        var facts = new TranscriptFacts((_, _) =>
        {
            gate.Wait(TimeSpan.FromSeconds(10));

            return new TranscriptFact(new TranscriptTail.Measurement(10, 20), "Opus 5.5");
        });
        var changes = 0;
        facts.Changed += (_, _) => Interlocked.Increment(ref changes);

        facts.Refresh([("a", false)]);

        Assert.That(facts.Known("a"), Is.EqualTo(default(TranscriptFact)), "the refresh must return before the read finishes");

        gate.Set();
        await facts.SettleAsync();

        Assert.That(facts.Known("a").Model, Is.EqualTo("Opus 5.5"));
        Assert.That(facts.Known("A").Measured.TotalBytes, Is.EqualTo(20), "session ids are not case sensitive");
        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public async Task Refresh_SaysNothingWhenNothingChanged()
    {
        var facts = new TranscriptFacts((_, _) => new TranscriptFact(default, "Opus 5.5"));
        var changes = 0;
        facts.Changed += (_, _) => Interlocked.Increment(ref changes);

        facts.Refresh([("a", true)]);
        await facts.SettleAsync();
        facts.Refresh([("a", true)]);
        await facts.SettleAsync();

        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public async Task Refresh_TellsTheReaderWhichSessionsAreRunning_AndSkipsCardsWithNoSession()
    {
        var asked = new List<(string, bool)>();
        var facts = new TranscriptFacts((id, live) =>
        {
            lock (asked)
            {
                asked.Add((id, live));
            }

            return default;
        });

        facts.Refresh([("running", true), (null, false), ("  ", false), ("stopped", false)]);
        await facts.SettleAsync();

        Assert.That(asked, Is.EqualTo(new[] { ("running", true), ("stopped", false) }));
    }

    [Test]
    public async Task Refresh_DuringARead_GetsOneMorePass_WithTheLatestSessions()
    {
        var gate = new ManualResetEventSlim();
        var asked = new List<string>();
        var facts = new TranscriptFacts((id, _) =>
        {
            gate.Wait(TimeSpan.FromSeconds(10));

            lock (asked)
            {
                asked.Add(id);
            }

            return default;
        });

        facts.Refresh([("first", false)]);
        facts.Refresh([("second", false)]);
        facts.Refresh([("third", false)]);
        gate.Set();
        await facts.SettleAsync();

        Assert.That(asked.Last(), Is.EqualTo("third"), "the last refresh asked for must always be read");
        Assert.That(asked, Has.Count.LessThanOrEqualTo(2), "refreshes that arrive during a read fold into one more pass");
    }

    [Test]
    public async Task AReadThatFails_IsSkipped_AndTheRestAreStillRead()
    {
        var facts = new TranscriptFacts((id, _) => id == "bad"
            ? throw new InvalidOperationException("boom")
            : new TranscriptFact(default, "Opus 5.5"));

        facts.Refresh([("bad", false), ("good", false)]);
        await facts.SettleAsync();

        Assert.That(facts.Known("bad"), Is.EqualTo(default(TranscriptFact)));
        Assert.That(facts.Known("good").Model, Is.EqualTo("Opus 5.5"));
    }
}
