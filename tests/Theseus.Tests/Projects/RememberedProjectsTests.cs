using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// A start before this run's first catalogue lands uses the projects that read last time, so the morning's resume opens
/// in the project's window; once the catalogue lands, only what reads now counts (PR 3 review, item 2).
/// </summary>
[TestFixture]
public class RememberedProjectsTests
{
    private const string Folder = @"C:\repos\home";

    private string _directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "theseus-remembered-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    private RememberedProjects Store() => new(Path.Combine(_directory, "readable-projects.json"));

    private static ProjectFileReading Readable(string name) =>
        ProjectFileReader.Read(name, "# P\n\n**Kit:** method-v0.2\n**Primary branch:** owner/p\n") with { Folder = Folder };

    private static BackgroundProjectReads Reads(RememberedProjects store, IReadOnlyList<ProjectFileReading> catalogue, SemaphoreSlim posted) =>
        new(_ => catalogue, action =>
        {
            action();
            posted.Release();
        }, store);

    [Test]
    public void BeforeTheFirstCatalogueLands_TheProjectsThatReadLastTime_Count()
    {
        var store = Store();
        store.Remember(["theseus-bootstrap"]);
        using var posted = new SemaphoreSlim(0);

        var reads = Reads(store, [Readable("theseus-bootstrap")], posted);

        Assert.That(reads.HasLanded, Is.False);
        Assert.That(reads.ReadableNames, Is.EquivalentTo(new[] { "theseus-bootstrap" }),
            "a start now is not held back, and still opens in the project's window");
    }

    [Test]
    public void OnceTheCatalogueLands_AProjectGoneUnreadableSince_NoLongerCounts_AndIsForgotten()
    {
        var store = Store();
        store.Remember(["theseus-bootstrap", "gone"]);
        using var posted = new SemaphoreSlim(0);
        var reads = Reads(store, [Readable("theseus-bootstrap"), ProjectFileReading.Unreadable("gone", "line 1: no **Kit:** line under the title") with { Folder = Folder }], posted);

        reads.Request(Folder);
        Assert.That(posted.Wait(TimeSpan.FromSeconds(10)), Is.True);

        Assert.That(reads.ReadableNames, Is.EquivalentTo(new[] { "theseus-bootstrap" }));
        Assert.That(store.Load(), Is.EquivalentTo(new[] { "theseus-bootstrap" }), "what the next run starts from");
    }

    [Test]
    public void WithNoFolderSet_NothingRemembered_Counts()
    {
        var store = Store();
        store.Remember(["theseus-bootstrap"]);
        using var posted = new SemaphoreSlim(0);
        var reads = Reads(store, [], posted);

        reads.Request(null);

        Assert.That(reads.ReadableNames, Is.Empty, "the first refresh with no projects folder lands at once (AC 13)");
    }

    [Test]
    public void AFileThatCannotBeRead_RemembersNothing()
    {
        File.WriteAllText(Path.Combine(_directory, "readable-projects.json"), "{ not json");

        Assert.That(Store().Load(), Is.Empty);
    }
}
