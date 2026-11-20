using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>The owner's spec acceptances, in Theseus's own store, keyed by project, slug and commit (spec AC 7).</summary>
[TestFixture]
public class SpecAcceptanceStoreTests
{
    private string _folder = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _folder = Path.Combine(Path.GetTempPath(), "theseus-acceptances-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_folder);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_folder, recursive: true);

    private SpecAcceptanceStore Store() => new(Path.Combine(_folder, "spec-acceptances.json"));

    private static SpecAcceptance Acceptance(string slug, string hash) =>
        new("a-project", slug, hash, new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero));

    [Test]
    public void NothingAccepted_IsNoAcceptance()
    {
        Assert.That(Store().Latest("a-project", "a-slug"), Is.Null);
    }

    [Test]
    public void AnAcceptance_IsReadBackByANewStore()
    {
        Store().Accept(Acceptance("a-slug", "hash-1"));

        Assert.That(Store().Latest("a-project", "a-slug"), Is.EqualTo(Acceptance("a-slug", "hash-1")));
    }

    [Test]
    public void TheLatestAcceptanceOfASlug_IsTheOneCompared()
    {
        var store = Store();
        store.Accept(Acceptance("a-slug", "hash-1"));
        store.Accept(Acceptance("other-slug", "hash-9"));
        store.Accept(Acceptance("a-slug", "hash-2"));

        Assert.That(store.Latest("a-project", "a-slug")!.ContentHash, Is.EqualTo("hash-2"));
        Assert.That(store.Load(), Has.Count.EqualTo(3), "earlier acceptances are kept, keyed by their text");
    }

    [Test]
    public void AcceptingTheSameTextAgain_ReplacesItsRecord()
    {
        var store = Store();
        store.Accept(Acceptance("a-slug", "hash-1"));
        store.Accept(Acceptance("a-slug", "hash-1"));

        Assert.That(store.Load(), Has.Count.EqualTo(1));
    }

    [Test]
    public void AFileThatCannotBeRead_IsTreatedAsEmpty()
    {
        File.WriteAllText(Path.Combine(_folder, "spec-acceptances.json"), "{ not json");

        Assert.That(Store().Latest("a-project", "a-slug"), Is.Null);
    }
}
