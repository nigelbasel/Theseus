using Theseus.Core.Links;
using Theseus.Core.Projects;
using Theseus.Core.Stopping;

namespace Theseus.Tests.Projects;

/// <summary>
/// What the owner does on the board reaches the session it concerns (1.1e AC 27, 27a): Accept starts a stopped card and
/// queues for a running one, the stop hook delivers what is queued, and events() lists only what is new.
/// </summary>
[TestFixture]
public class OwnerEventsTests
{
    private static readonly DateTimeOffset Nine = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);
    private string _folder = string.Empty;

    [SetUp]
    public void MakeFolder()
    {
        _folder = Path.Combine(Path.GetTempPath(), $"theseus-owner-events-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void RemoveFolder() => Directory.Delete(_folder, recursive: true);

    private static SpecAtGate Spec(bool rule = false) =>
        new("bym", "w7", SpecIdentity.Hash("v2"), "v2", new WorkstreamSource(@"<projects>\bym\work\w7")) { IsRule = rule };

    private const string Session = "0b6f4d3c-2a1e-4c55-9d7a-6f1e2b3c4d5e";

    private SessionTells Tells(out StopRequestStore waiting, out OwnerEventLog log)
    {
        waiting = new StopRequestStore(Path.Combine(_folder, "tells"));
        log = new OwnerEventLog(Path.Combine(_folder, "owner-events.jsonl"));

        return new SessionTells(waiting, log);
    }

    [Test]
    public void Accept_QueuesForARunningCard_AndLeavesAStoppedOneToBeStarted_LoggingBoth()
    {
        var tells = Tells(out var waiting, out var log);
        var text = AcceptedTell.Text(Spec());

        Assert.That(text, Is.EqualTo($"Your spec (version {Spec().ShortVersion}) was accepted: carry on to the plan."));
        Assert.That(AcceptedTell.Text(Spec(rule: true)), Does.StartWith("Your rule"));
        Assert.That(tells.Tell("w7", "W7", isRunning: false, Session, text, Nine), Is.False, "a stopped card is started with the text instead");
        Assert.That(tells.Tell("w7", "W7", isRunning: true, "not-a-session-id", text, Nine), Is.False, "nowhere to queue it, so it is started");
        Assert.That(waiting.Waiting(Session), Is.Null);
        Assert.That(tells.Tell("w7", "W7", isRunning: true, Session, text, Nine), Is.True);
        Assert.That((waiting.Waiting(Session)!.Text, waiting.Waiting(Session)!.ExpiresUtc), Is.EqualTo((text, Nine + AcceptedTell.Waits)));
        Assert.That(log.All().Count, Is.EqualTo(3), "every one is logged for events()");
    }

    [Test]
    public void SeveralTells_AreJoined_AndAStartTakesThem()
    {
        var tells = Tells(out var waiting, out _);
        tells.Tell("w7", "W7", true, Session, "first", Nine);
        tells.Tell("w7", "W7", true, Session, "second", Nine);

        Assert.That(waiting.Waiting(Session)!.Text, Is.EqualTo($"first{Environment.NewLine}{Environment.NewLine}second"));
        Assert.That(tells.TakeForStart(Session, Nine), Is.EqualTo($"first{Environment.NewLine}{Environment.NewLine}second"));
        Assert.That((waiting.Waiting(Session), waiting.Delivered(Session), tells.TakeForStart(Session, Nine)),
            Is.EqualTo(((PendingStopRequest?)null, (PendingStopRequest?)null, (string?)null)), "taken once, nothing left behind");
    }

    [Test]
    public void TheHook_DeliversAStopFirst_ThenATell_EachOnce()
    {
        var stops = new StopRequestStore(Path.Combine(_folder, "stops"));
        var tells = Tells(out var waiting, out _);
        tells.Tell("w7", "W7", true, Session, "Your spec was accepted", Nine);
        stops.Write(new PendingStopRequest(Session, "w7", "W7", "Please stop", Nine, Nine.AddHours(1)));
        var input = StopHookInput.Parse($$"""{"session_id":"{{Session}}","hook_event_name":"Stop"}""");

        Assert.That(HookTurn.Deliver(input, stops, waiting, Nine)?.Text, Is.EqualTo("Please stop"));
        Assert.That(HookTurn.Deliver(input, stops, waiting, Nine)?.Text, Is.EqualTo("Your spec was accepted"));
        Assert.That(HookTurn.Deliver(input, stops, waiting, Nine), Is.Null);
        Assert.That(StopRequestRoot.Tells, Is.Not.EqualTo(StopRequestRoot.Default));
    }

    [Test]
    public void Events_GivesOnlyWhatIsNewSinceTheSessionLastAsked_ForItsOwnCard()
    {
        var log = new OwnerEventLog(Path.Combine(_folder, "owner-events.jsonl"));
        log.Add(new OwnerEvent("w7", OwnerEvents.Approved("Cache spike", "Cache spike", "cache-spike"), Nine));
        log.Add(new OwnerEvent("w8", "not yours", Nine));

        Assert.That(log.TakeNew("w7", Nine.AddMinutes(1)).Select(owned => owned.Text),
            Is.EqualTo(new[] { "The owner approved your proposal \"Cache spike\" on the board: its card is Cache spike (cache-spike)." }));
        Assert.That(log.TakeNew("w7", Nine.AddMinutes(2)), Is.Empty);

        log.Add(new OwnerEvent("w7", OwnerEvents.Rejected("Other", "Not now"), Nine.AddMinutes(3)));

        Assert.That(OwnerEvents.Describe(log.TakeNew("w7", Nine.AddMinutes(4)), TimeZoneInfo.Utc),
            Is.EqualTo("22 Nov 09:03: The owner rejected your proposal \"Other\" on the board: Not now"));
        Assert.That(OwnerEvents.Describe([], TimeZoneInfo.Utc), Is.EqualTo("Nothing new from the owner on the board since you last asked."));
    }

    [TestCase(LinkKind.ReportsBack, "The owner confirmed W8's report to you on the board: <records>\\REPORT.md")]
    [TestCase(LinkKind.HandsOver, "The owner accepted W8's hand-over to you on the board: <records>\\REPORT.md")]
    public void AConfirmedReport_TellsItsRecipient(LinkKind kind, string text)
    {
        Assert.That(OwnerEvents.Confirmed(kind, "W8", @"<records>\REPORT.md"), Is.EqualTo(text));
    }
}
