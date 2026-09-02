using Theseus.Core.Stopping;

namespace Theseus.Tests.Stopping;

[TestFixture]
public class StopRequestStoreTests
{
    private const string SessionId = "11111111-2222-3333-4444-555555555555";

    private string _root = string.Empty;
    private StopRequestStore _store = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-stop-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _store = new StopRequestStore(_root);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Claim_HandsTheRequestToExactlyOneCaller()
    {
        _store.Write(Request());

        Assert.That(_store.Claim(SessionId), Is.Not.Null);
        Assert.That(_store.Claim(SessionId), Is.Null,
            "a session told twice would be interrupted on its way out of writing the handoff");
    }

    [Test]
    public void Claim_ReturnsWhatWasAskedFor()
    {
        _store.Write(Request());

        var claimed = _store.Claim(SessionId)!;

        Assert.That(claimed.Text, Is.EqualTo("Write a handoff and stop."));
        Assert.That(claimed.WorkstreamKey, Is.EqualTo("contact-forms"));
    }

    [Test]
    public void Claim_FindsNothingForASessionNobodyAskedToStop()
    {
        Assert.That(_store.Claim(SessionId), Is.Null);
    }

    [Test]
    public void Claim_RefusesAnIdentifierThatIsNotASessionId()
    {
        _store.Write(Request());

        Assert.That(_store.Claim(@"..\..\windows\system32"), Is.Null,
            "the identifier arrives from outside, so it must never become a path unchecked");
    }

    [Test]
    public void Cancel_WithdrawsARequestThatHasNotBeenGivenYet()
    {
        _store.Write(Request());

        Assert.That(_store.Cancel(SessionId), Is.True);
        Assert.That(_store.Claim(SessionId), Is.Null);
    }

    [Test]
    public void Cancel_CannotTakeBackARequestAlreadyGiven()
    {
        _store.Write(Request());
        _store.Claim(SessionId);

        Assert.That(_store.Cancel(SessionId), Is.False);
    }

    [Test]
    public void Write_ReplacesARequestThatHasNotBeenGivenYet()
    {
        _store.Write(Request());
        _store.Write(Request() with { Text = "Different words." });

        Assert.That(_store.Claim(SessionId)!.Text, Is.EqualTo("Different words."));
        Assert.That(_store.Pending(), Is.Empty);
    }

    [Test]
    public void Pending_ListsWhatIsStillWaiting()
    {
        _store.Write(Request());

        Assert.That(_store.Pending(), Has.Count.EqualTo(1));

        _store.Claim(SessionId);

        Assert.That(_store.Pending(), Is.Empty);
    }

    [Test]
    public void Pending_IsEmptyBeforeAnythingHasEverBeenAsked()
    {
        Assert.That(_store.Pending(), Is.Empty);
    }

    private static PendingStopRequest Request() =>
        new(SessionId, "contact-forms", "Contact forms", "Write a handoff and stop.",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddHours(2));

    /// <summary>
    /// A request that has been acted on must not survive to be acted on again.
    ///
    /// The only thing preventing a second `/exit` was a HashSet held in memory, so it was empty
    /// again in the next process while the request sat on disk for four hours. On 2026-09-02 that
    /// typed `/exit` into the same sessions repeatedly across restarts - one window was closed twice,
    /// hours apart, the second time long after it had been resumed and was doing other work.
    /// </summary>
    [Test]
    public void Retire_LeavesNothingForTheNextTheseusToActOnAgain()
    {
        _store.Write(Request());
        _store.Claim(SessionId);

        Assert.That(_store.Delivered(SessionId), Is.Not.Null, "the session was handed its request");
        Assert.That(_store.Retire(SessionId), Is.True);

        Assert.That(_store.Delivered(SessionId), Is.Null,
            "a request still on disk is one the next process types /exit for all over again");
    }

    /// <summary>
    /// What the board asks about on startup: stops that were delivered and never finished, because
    /// Theseus closed before it could type the exit. Left to itself it completes them within a second
    /// of the next launch, which is how two of Nigel's sessions were closed 0.8s after the window
    /// appeared on 2026-09-02.
    /// </summary>
    [Test]
    public void Handed_NamesTheStopsNobodyFinished()
    {
        _store.Write(Request());
        _store.Claim(SessionId);

        var handed = _store.Handed();

        Assert.That(handed, Has.Count.EqualTo(1));
        Assert.That(handed[0].SessionId, Is.EqualTo(SessionId));
    }

    [Test]
    public void Handed_IsEmptyWhileARequestIsStillWaitingToBeGiven()
    {
        _store.Write(Request());

        Assert.That(_store.Handed(), Is.Empty,
            "a request nobody has been given is not an unfinished stop, it is a stop that has not started");
    }

    [Test]
    public void Handed_ForgetsARequestOnceItHasBeenFinishedWith()
    {
        _store.Write(Request());
        _store.Claim(SessionId);
        _store.Retire(SessionId);

        Assert.That(_store.Handed(), Is.Empty);
    }

    [Test]
    public void Handed_IsEmptyBeforeAnythingHasEverBeenAsked()
    {
        Assert.That(_store.Handed(), Is.Empty);
    }

    [Test]
    public void Retire_SaysNoWhenThereWasNothingToFinishWith()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_store.Retire(SessionId), Is.False, "nothing was ever delivered");
            Assert.That(_store.Retire(null), Is.False);
            Assert.That(_store.Retire("not-a-session-id"), Is.False);
        });
    }

    [Test]
    public void Retire_LeavesAnUndeliveredRequestWhereItIs()
    {
        _store.Write(Request());

        Assert.Multiple(() =>
        {
            Assert.That(_store.Retire(SessionId), Is.False);
            Assert.That(_store.Waiting(SessionId), Is.Not.Null,
                "a request nobody has been given yet still has its whole job ahead of it");
        });
    }

    [Test]
    public void Sweep_ExpiresADeliveredRequestThatWasNeverActedOn()
    {
        _store.Write(Request());
        _store.Claim(SessionId);

        Assert.That(_store.Sweep(DateTimeOffset.UtcNow.AddHours(3)), Is.EqualTo(1),
            "sweeping only the waiting ones left delivered requests armed for days");
        Assert.That(_store.Delivered(SessionId), Is.Null);
    }

    [Test]
    public void Sweep_LeavesADeliveredRequestThatIsStillLive()
    {
        _store.Write(Request());
        _store.Claim(SessionId);

        Assert.Multiple(() =>
        {
            Assert.That(_store.Sweep(DateTimeOffset.UtcNow), Is.Zero);
            Assert.That(_store.Delivered(SessionId), Is.Not.Null,
                "a session that has not yet handed over still has something owed to it");
        });
    }

    [Test]
    public void Waiting_LooksWithoutTaking()
    {
        _store.Write(Request());

        Assert.That(_store.Waiting(SessionId), Is.Not.Null);
        Assert.That(_store.Claim(SessionId), Is.Not.Null,
            "a hook must be able to decide before it claims, or a turn it declines eats the request");
    }

    [Test]
    public void Waiting_FindsNothingOnceTheRequestHasBeenTaken()
    {
        _store.Write(Request());
        _store.Claim(SessionId);

        Assert.That(_store.Waiting(SessionId), Is.Null);
    }

    [Test]
    public void Waiting_RefusesAnIdentifierThatIsNotASessionId()
    {
        Assert.That(_store.Waiting(@"..\..\windows"), Is.Null);
    }

    [Test]
    public void Sweep_DiscardsARequestNothingEverCameBackFor()
    {
        _store.Write(Request() with { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(-1) });

        Assert.That(_store.Sweep(DateTimeOffset.UtcNow), Is.EqualTo(1));
        Assert.That(_store.Pending(), Is.Empty,
            "a request the hook will refuse to deliver would otherwise sit there for ever");
    }

    [Test]
    public void Sweep_LeavesARequestThatIsStillWorthDelivering()
    {
        _store.Write(Request());

        Assert.That(_store.Sweep(DateTimeOffset.UtcNow), Is.Zero);
        Assert.That(_store.Pending(), Has.Count.EqualTo(1));
    }

    [Test]
    public void Sweep_CopesWithNothingHavingBeenAskedAtAll()
    {
        Assert.That(_store.Sweep(DateTimeOffset.UtcNow), Is.Zero);
    }
}
