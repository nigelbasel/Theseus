using System.Collections.Specialized;
using Theseus.App.ViewModels;
using Theseus.Core.Locks;

namespace Theseus.Tests.ViewModels;

[TestFixture]
public class LockRowViewModelTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void Update_ShowsTheHolderAndHowLong()
    {
        var row = Row();

        row.Update([Holder("Legacy job migration", "PersistenceTests.Jobs", Nine.AddMinutes(-42))], Nine);

        Assert.That(row.IsHeld, Is.True);
        Assert.That(row.Holder, Is.EqualTo("Legacy job migration — PersistenceTests.Jobs"));
        Assert.That(row.HeldFor, Is.EqualTo("42m"));
        Assert.That(row.IsLong, Is.False);
        Assert.That(row.Summary, Is.EqualTo("tests: held 42m"));
    }

    [Test]
    public void Update_FlagsAHoldLongerThanTheThreshold()
    {
        var row = Row();

        row.Update([Holder("Legacy job migration", "PersistenceTests.Jobs", Nine - LocksViewModel.LongHold - TimeSpan.FromMinutes(1))], Nine);

        Assert.That(row.IsLong, Is.True);
    }

    [Test]
    public void Update_ListsTheQueueInOrderWithPositions()
    {
        var row = Row();

        row.Update(
        [
            Holder("Legacy job migration", "PersistenceTests.Jobs", Nine.AddMinutes(-5)),
            Waiter("a", "NDC-1190", Nine.AddMinutes(-4), "blocking a release"),
            Waiter("b", "GDPR", Nine.AddMinutes(-2)),
        ], Nine);

        Assert.That(row.Waiting.Select(waiter => waiter.Who), Is.EqualTo(new[] { "NDC-1190", "GDPR" }));
        Assert.That(row.Waiting[0].Position, Is.EqualTo(1));
        Assert.That(row.Waiting[0].Reason, Is.EqualTo("blocking a release"));
        Assert.That(row.Waiting[0].MoveUpCommand.CanExecute(null), Is.False);
        Assert.That(row.Waiting[1].MoveDownCommand.CanExecute(null), Is.False);
        Assert.That(row.Summary, Is.EqualTo("tests: held 5m, 2 waiting"));
    }

    [Test]
    public void Update_WithNothingChanged_RaisesNoCollectionChanges()
    {
        var row = Row();
        var tickets = new[] { Holder("Legacy job migration", "PersistenceTests.Jobs", Nine), Waiter("a", "NDC-1190", Nine), Waiter("b", "GDPR", Nine) };
        row.Update(tickets, Nine);
        var changes = 0;
        ((INotifyCollectionChanged)row.Waiting).CollectionChanged += (_, _) => changes++;

        row.Update(tickets, Nine.AddMinutes(1));

        Assert.That(changes, Is.Zero);
    }

    [Test]
    public void Update_FollowsAReorderByMovingTheSameEntry()
    {
        var row = Row();
        row.Update([Holder("H", "x", Nine), Waiter("a", "NDC-1190", Nine), Waiter("b", "GDPR", Nine)], Nine);
        var gdpr = row.Waiting[1];

        row.Update([Holder("H", "x", Nine), Waiter("b", "GDPR", Nine), Waiter("a", "NDC-1190", Nine)], Nine);

        Assert.That(row.Waiting[0], Is.SameAs(gdpr));
        Assert.That(gdpr.Position, Is.EqualTo(1));
    }

    [Test]
    public void AFreeLockWithNobodyWaiting_IsNotShown()
    {
        var row = Row();

        row.Update([], Nine);

        Assert.That(row.IsActive, Is.False);
        Assert.That(row.Summary, Is.Empty);
    }

    [Test]
    public void Release_AsksBeforeItActs()
    {
        var released = new List<string>();
        var row = new LockRowViewModel(LockNames.Tests, released.Add, _ => { }, _ => { }, _ => { });
        row.Update([Holder("H", "x", Nine)], Nine);

        row.AskReleaseCommand.Execute(null);
        Assert.That(released, Is.Empty);
        Assert.That(row.CanAskRelease, Is.False);

        row.ConfirmReleaseCommand.Execute(null);
        Assert.That(released, Is.EqualTo(new[] { LockNames.Tests }));
    }

    private static LockRowViewModel Row() => new(LockNames.Tests, _ => { }, _ => { }, _ => { }, _ => { });

    private static LockTicket Holder(string who, string purpose, DateTimeOffset granted) =>
        new("holder", LockNames.Tests, who, purpose, HoldMode.Process, "h", granted, granted, GrantedUtc: granted);

    private static LockTicket Waiter(string token, string who, DateTimeOffset requested, string? reason = null) =>
        new(token, LockNames.Tests, who, "suite", HoldMode.Process, token, requested, requested, Reason: reason);
}
