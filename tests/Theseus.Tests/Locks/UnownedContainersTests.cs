using Theseus.Core.Locks;

namespace Theseus.Tests.Locks;

[TestFixture]
public class UnownedContainersTests
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void WithTheTestsLockFree_EveryContainerIsUnowned()
    {
        var unowned = UnownedContainers.From([Container("old", Nine.AddDays(-4)), Container("new", Nine)], []);

        Assert.That(unowned.Select(container => container.Id), Is.EqualTo(new[] { "old", "new" }));
    }

    [Test]
    public void ContainersCreatedDuringTheCurrentHold_BelongToItsHolder()
    {
        var holder = new LockTicket("t", LockNames.Tests, "Legacy job migration", "suite", HoldMode.Process, "k",
            Nine.AddMinutes(-10), Nine, GrantedUtc: Nine.AddMinutes(-10), ProcessId: 1, ProcessStart: "s");

        var unowned = UnownedContainers.From([Container("leftover", Nine.AddDays(-4)), Container("its-sql", Nine.AddMinutes(-5))], [holder]);

        Assert.That(unowned.Select(container => container.Id), Is.EqualTo(new[] { "leftover" }));
    }

    [Test]
    public void AHoldOnAnotherLock_OwnsNoTestContainers()
    {
        var llama = new LockTicket("t", LockNames.Llama, "Someone", "classify", HoldMode.Session, "k",
            Nine.AddHours(-1), Nine, SessionId: "s", GrantedUtc: Nine.AddHours(-1));

        Assert.That(UnownedContainers.From([Container("c", Nine.AddMinutes(-5))], [llama]), Has.Count.EqualTo(1));
    }

    [Test]
    public void DockersListing_IsReadIntoContainers()
    {
        Assert.That(DockerTestContainers.ParseCreated("2026-10-03 17:02:10 -0100 X"),
            Is.EqualTo(new DateTimeOffset(2026, 10, 3, 18, 2, 10, TimeSpan.Zero)));
    }

    private static TestContainer Container(string id, DateTimeOffset created) =>
        new(id, $"name-{id}", "mcr.microsoft.com/mssql/server:2022-latest", created, "Up 3 hours");
}
