using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class SessionMenuTests
{
    [Test]
    public void Entries_ListsOnlySessionsSomeoneCouldBeTakenTo()
    {
        var entries = SessionMenu.Entries([Session("a", "Alpha"), Session("b", "Scripted", entrypoint: "sdk-cli")]);

        Assert.That(entries.Select(entry => entry.SessionId), Is.EqualTo(new[] { "a" }));
    }

    [Test]
    public void Unchanged_IsTrueForTheSameSessionsReadAgain()
    {
        var applied = SessionMenu.Entries([Session("a", "Alpha"), Session("b", "Beta")]);
        var readAgain = SessionMenu.Entries([Session("a", "Alpha", status: "busy"), Session("b", "Beta", status: "idle")]);

        Assert.That(SessionMenu.Unchanged(applied, readAgain), Is.True,
            "a session changing between busy and idle is not a reason to commit the jump list");
    }

    [Test]
    public void Unchanged_IsFalseWhenASessionIsRenamed()
    {
        var applied = SessionMenu.Entries([Session("a", "Alpha")]);

        Assert.That(SessionMenu.Unchanged(applied, SessionMenu.Entries([Session("a", "Alpha renamed")])), Is.False);
    }

    [Test]
    public void Unchanged_IsFalseWhenASessionStartsOrEnds()
    {
        var one = SessionMenu.Entries([Session("a", "Alpha")]);
        var two = SessionMenu.Entries([Session("a", "Alpha"), Session("b", "Beta")]);

        Assert.That(SessionMenu.Unchanged(one, two), Is.False);
        Assert.That(SessionMenu.Unchanged(two, one), Is.False);
    }

    [Test]
    public void Unchanged_IsFalseBeforeAnythingHasBeenApplied()
    {
        Assert.That(SessionMenu.Unchanged(null, []), Is.False, "an empty menu still has to be committed once");
    }

    private static LiveSession Session(string id, string name, string status = "idle", string entrypoint = "cli") =>
        new(1000, id, @"C:\work", name, status, entrypoint, new DateTimeOffset(2026, 9, 15, 9, 0, 0, TimeSpan.Zero),
            LastSeenUtc: DateTimeOffset.UtcNow);
}
