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
