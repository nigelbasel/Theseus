using Theseus.Core.Resources;

namespace Theseus.Tests.Resources;

/// <summary>
/// Two process tables a few seconds apart give each process's rates, and a session is its Claude Code
/// process plus everything started beneath it.
/// </summary>
[TestFixture]
public class ResourceSamplerTests
{
    private static readonly DateTimeOffset First = new(2026, 11, 6, 21, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Second = First.AddSeconds(5);
    private static readonly long Old = First.AddMinutes(-30).ToFileTime();
    private static readonly long Older = First.AddHours(-2).ToFileTime();

    private const long Second1 = TimeSpan.TicksPerSecond;
    private const long Mb = 1L << 20;

    [Test]
    public void TheFirstReading_HasNoRates_ButCountsProcessesAndMemory()
    {
        var sampler = new ResourceSampler(4);

        var reading = sampler.Take(Table(First, 0, 0, Claude(100, cpu: 0, io: 0), Child(200, 100, "bash")), [100]);
        var session = reading.Session(100)!;

        Assert.That(reading.HasRates, Is.False);
        Assert.That(session.ProcessCount, Is.EqualTo(2));
        Assert.That(session.PrivateBytes, Is.EqualTo(300 * Mb));
        Assert.That(session.CpuPercent, Is.Zero);
    }

    [Test]
    public void ProcessorTimeAndIo_AreRatesOverTheGapBetweenReadings_AsAShareOfTheWholeMachine()
    {
        var sampler = new ResourceSampler(4);
        sampler.Take(Table(First, 0, 0, Claude(100, cpu: 10 * Second1, io: 1000)), [100]);

        var reading = sampler.Take(Table(Second, 0, 0, Claude(100, cpu: 12 * Second1, io: 1500)), [100]);
        var session = reading.Session(100)!;

        Assert.That(reading.HasRates, Is.True);
        Assert.That(session.CpuPercent, Is.EqualTo(10).Within(0.001), "2s of processor time in 5s on 4 processors");
        Assert.That(session.IoPerSecond, Is.EqualTo(100).Within(0.001));
    }

    [Test]
    public void ASessionsTree_TakesEveryDescendant_AndNothingElse()
    {
        var sampler = new ResourceSampler(4);

        var reading = sampler.Take(Table(First, 0, 0,
            Claude(100, 0, 0),
            Child(200, 100, "bash"),
            Child(300, 200, "dotnet"),
            Child(400, 300, "testhost"),
            Child(500, 999, "chrome")), [100]);

        Assert.That(reading.Session(100)!.ProcessCount, Is.EqualTo(4));
    }

    [Test]
    public void AProcessThatNamesAReusedIdAsItsParent_IsNotTakenIntoTheTree()
    {
        var sampler = new ResourceSampler(4);

        var reading = sampler.Take(Table(First, 0, 0,
            Claude(100, 0, 0),
            new ProcessCounters(600, 100, "stale", Older, 0, 50 * Mb, 0)), [100]);

        Assert.That(reading.Session(100)!.ProcessCount, Is.EqualTo(1), "it started before the session did, so its parent was another process");
    }

    [Test]
    public void AProcessStartedBetweenReadings_CountsEverythingItHasUsed()
    {
        var sampler = new ResourceSampler(1);
        sampler.Take(Table(First, 0, 0, Claude(100, 0, 0)), [100]);

        var born = First.AddSeconds(2).ToFileTime();
        var reading = sampler.Take(Table(Second, 0, 0,
            Claude(100, 0, 0),
            new ProcessCounters(700, 100, "dotnet", born, Second1, 10 * Mb, 50)), [100]);

        Assert.That(reading.Session(100)!.CpuPercent, Is.EqualTo(20).Within(0.001));
        Assert.That(reading.Session(100)!.IoPerSecond, Is.EqualTo(10).Within(0.001));
    }

    [Test]
    public void AnIdReusedBetweenReadings_IsNotComparedWithTheProcessThatHadItBefore()
    {
        var sampler = new ResourceSampler(1);
        sampler.Take(Table(First, 0, 0, Claude(100, 0, 0), new ProcessCounters(800, 100, "node", Old, 100 * Second1, Mb, 0)), [100]);

        var reading = sampler.Take(Table(Second, 0, 0,
            Claude(100, 0, 0),
            new ProcessCounters(800, 100, "node", Old + 1, 101 * Second1, Mb, 0)), [100]);

        Assert.That(reading.Session(100)!.CpuPercent, Is.Zero, "a different process, already running before the first reading, has no rate yet");
    }

    [Test]
    public void TheMachine_ShowsHowBusyItIs_AndWhatDefenderDockerAndTheSessionsUse()
    {
        var sampler = new ResourceSampler(2);
        sampler.Take(Table(First, idle: 0, total: 0,
            Claude(100, 0, 0),
            new ProcessCounters(10, 4, "MsMpEng", Older, 0, 0, 0),
            new ProcessCounters(11, 4, "vmmemWSL", Older, 0, 0, 0)), [100]);

        var reading = sampler.Take(Table(Second, idle: 4 * Second1, total: 10 * Second1,
            Claude(100, Second1, 0),
            new ProcessCounters(10, 4, "MsMpEng", Older, 2 * Second1, 0, 0),
            new ProcessCounters(11, 4, "vmmemWSL", Older, 3 * Second1, 0, 0)), [100]);

        Assert.That(reading.Machine.CpuPercent, Is.EqualTo(60).Within(0.001));
        Assert.That(reading.Machine.DefenderPercent, Is.EqualTo(20).Within(0.001));
        Assert.That(reading.Machine.DockerPercent, Is.EqualTo(30).Within(0.001));
        Assert.That(reading.Machine.SessionsPercent, Is.EqualTo(10).Within(0.001));
    }

    [Test]
    public void ASessionWhoseProcessHasGone_HasNoReading()
    {
        var reading = new ResourceSampler(4).Take(Table(First, 0, 0, Claude(100, 0, 0)), [100]);

        Assert.That(reading.Session(555), Is.Null);
        Assert.That(reading.Session(null), Is.Null);
    }

    [Test]
    public void TheBusiest_GroupProcessesOfOneName_BusiestFirst()
    {
        var sampler = new ResourceSampler(1);
        sampler.Take(Table(First, 0, 0, Claude(100, 0, 0), Child(200, 100, "dotnet"), Child(201, 100, "dotnet")), [100]);

        var reading = sampler.Take(Table(Second, 0, 0,
            Claude(100, 0, 0),
            Child(200, 100, "dotnet", cpu: Second1),
            Child(201, 100, "dotnet", cpu: Second1)), [100]);
        var busiest = reading.Session(100)!.Busiest;

        Assert.That(busiest[0].Name, Is.EqualTo("dotnet ×2"));
        Assert.That(busiest[0].CpuPercent, Is.EqualTo(40).Within(0.001));
        Assert.That(busiest[1].Name, Is.EqualTo("claude"));
    }

    private static ProcessTable Table(DateTimeOffset at, long idle, long total, params ProcessCounters[] processes) =>
        new(at, processes, idle, total);

    private static ProcessCounters Claude(int id, long cpu, long io) =>
        new(id, 1, "claude", Old, cpu, 200 * Mb, io);

    private static ProcessCounters Child(int id, int parent, string name, long cpu = 0) =>
        new(id, parent, name, Old + id, cpu, 100 * Mb, 0);
}
