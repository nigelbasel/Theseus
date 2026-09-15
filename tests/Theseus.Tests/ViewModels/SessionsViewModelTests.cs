using Theseus.App.ViewModels;
using Theseus.Core.Sessions;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A refresh of the live sessions keeps the rows that are there.
///
/// It used to clear the list and refill it, which raised a change for every row on every refresh,
/// and the taskbar jump list was committed on each of those changes - a blocking call into the shell
/// that froze the board on 2026-09-15.
/// </summary>
[TestFixture]
public class SessionsViewModelTests
{
    private static readonly DateTimeOffset Morning = new(2026, 9, 15, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void Refresh_KeepsEveryRowAndChangesNothingWhenTheSameSessionsAreReadAgain()
    {
        var registry = new FakeRegistry(Session(1, "a", minutes: 0), Session(2, "b", minutes: 5));
        var panel = new SessionsViewModel(registry, new FakeTitles());
        panel.Refresh();
        var before = panel.Sessions.ToList();
        var changes = 0;
        panel.Sessions.CollectionChanged += (_, _) => changes++;

        registry.Live = [Session(1, "a", minutes: 0, status: "busy"), Session(2, "b", minutes: 5, status: "idle")];
        panel.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(changes, Is.Zero, "a session changing between busy and idle is not a change to the list");
            Assert.That(panel.Sessions.Zip(before).All(pair => ReferenceEquals(pair.First, pair.Second)), Is.True);
            Assert.That(panel.Sessions.Single(row => row.SessionId == "a").Status, Is.EqualTo("busy"));
        });
    }

    [Test]
    public void Refresh_AddsAndRemovesOnlyTheSessionsThatStartedOrEnded()
    {
        var registry = new FakeRegistry(Session(1, "a", minutes: 0), Session(2, "b", minutes: 5));
        var panel = new SessionsViewModel(registry, new FakeTitles());
        panel.Refresh();
        var kept = panel.Sessions.Single(row => row.SessionId == "b");
        var changes = 0;
        panel.Sessions.CollectionChanged += (_, _) => changes++;

        registry.Live = [Session(2, "b", minutes: 5), Session(3, "c", minutes: 10)];
        panel.Refresh();

        Assert.Multiple(() =>
        {
            Assert.That(changes, Is.EqualTo(2), "one session ended and one started");
            Assert.That(panel.Sessions.Select(row => row.SessionId), Is.EqualTo(new[] { "c", "b" }), "newest first");
            Assert.That(panel.Sessions.Single(row => row.SessionId == "b"), Is.SameAs(kept));
        });
    }

    [Test]
    public void Refresh_TakesARenamedTabOnTheSameRow()
    {
        var titles = new FakeTitles();
        var registry = new FakeRegistry(Session(1, "a", minutes: 0));
        var panel = new SessionsViewModel(registry, titles);
        panel.Refresh();
        var row = panel.Sessions.Single();

        titles.Titles[1] = "Renamed tab";
        panel.Refresh();

        Assert.That(panel.Sessions.Single(), Is.SameAs(row));
        Assert.That(row.Name, Is.EqualTo("Renamed tab"));
    }

    [Test]
    public void Refresh_TellsTwoProcessesOnOneSessionApart()
    {
        var registry = new FakeRegistry(Session(1, "forked", minutes: 0), Session(2, "forked", minutes: 1));
        var panel = new SessionsViewModel(registry, new FakeTitles());

        panel.Refresh();
        panel.Refresh();

        Assert.That(panel.Sessions.Select(row => row.Pid), Is.EquivalentTo(new[] { 1, 2 }));
    }

    [Test]
    public void Refresh_SaysOnceThatItHasFinished()
    {
        var registry = new FakeRegistry(Session(1, "a", minutes: 0), Session(2, "b", minutes: 5), Session(3, "c", minutes: 9));
        var panel = new SessionsViewModel(registry, new FakeTitles());
        var finished = 0;
        panel.Refreshed += (_, _) => finished++;

        panel.Refresh();

        Assert.That(finished, Is.EqualTo(1), "the jump list is committed once a refresh, not once a row");
    }

    private static LiveSession Session(int pid, string id, int minutes, string status = "idle") =>
        new(pid, id, @"C:\work", $"session {id}", status, "cli", Morning.AddMinutes(minutes));

    private sealed class FakeRegistry(params LiveSession[] live) : ISessionRegistryReader
    {
        public IReadOnlyList<LiveSession> Live { get; set; } = live;

        public SessionRegistrySnapshot Read() => new(Live, 0, 0);
    }

    private sealed class FakeTitles : IConsoleTitleReader
    {
        public Dictionary<int, string> Titles { get; } = [];

        public string? Read(int processId) => Titles.GetValueOrDefault(processId);
    }
}
