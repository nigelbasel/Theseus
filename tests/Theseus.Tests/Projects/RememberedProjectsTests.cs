using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>
/// A start before this run's first read lands uses the members remembered from last time, so the morning's resume opens
/// in the project's window; once the read lands, only what the files say now counts (1.1 PR 3 review, item 2; 1.1c).
/// </summary>
[TestFixture]
public class RememberedProjectsTests
{
    private const string Folder = @"C:\projects";

    private string _directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), "theseus-remembered-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_directory, recursive: true);

    private RememberedProjects Store() => new(Path.Combine(_directory, "project-members.json"));

    private static ProjectFileReading Naming(string project, string card, string slug) =>
        ProjectFileReader.Read(project, TestProjectsFolder.ProjectText(project, body:
            "## Phase 0: Start\n\n### Objective 0.1: Something\n- [ ] A criterion.\n\n**Pipeline:** feature\n"
            + "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n"
            + $"| {card} | {slug} | owner/{slug} | a-repo |\n")) with { Folder = Folder };

    private static BackgroundProjectReads Reads(RememberedProjects store, IReadOnlyList<ProjectFileReading> catalogue, SemaphoreSlim posted) =>
        new(_ => catalogue, action =>
        {
            action();
            posted.Release();
        }, store);

    [Test]
    public void BeforeTheFirstReadLands_TheMembersOfLastTime_Count()
    {
        var store = Store();
        store.Remember(ProjectMembers.From([Naming("theseus", "git-22", "a-slug")]));
        using var posted = new SemaphoreSlim(0);

        var reads = Reads(store, [], posted);

        Assert.That(reads.HasLanded, Is.False);
        Assert.That(reads.Members.Of("git-22"), Is.EqualTo(new ProjectMember("theseus", "a-slug")),
            "a start now is not held back, and still opens in the project's window");
        Assert.That(reads.ReadableNames, Is.EquivalentTo(new[] { "theseus" }));
    }

    [Test]
    public void OnceTheReadLands_OnlyWhatTheFilesSayNow_Counts_AndIsRemembered()
    {
        var store = Store();
        store.Remember(ProjectMembers.From([Naming("theseus", "gone-card", "old-slug")]));
        using var posted = new SemaphoreSlim(0);
        var reads = Reads(store, [Naming("theseus", "git-22", "a-slug")], posted);

        reads.Request(Folder);
        Assert.That(posted.Wait(TimeSpan.FromSeconds(10)), Is.True);

        Assert.That(reads.Members.Of("gone-card"), Is.Null);
        Assert.That(store.Load().ByCard.Keys, Is.EquivalentTo(new[] { "git-22" }), "what the next run starts from");
    }

    [Test]
    public void WithNoFolderSet_NothingRemembered_Counts()
    {
        var store = Store();
        store.Remember(ProjectMembers.From([Naming("theseus", "git-22", "a-slug")]));
        using var posted = new SemaphoreSlim(0);
        var reads = Reads(store, [], posted);

        reads.Request(null);

        Assert.That((reads.Members.ByCard.Count, reads.ReadableNames.Count), Is.EqualTo((0, 0)),
            "the first refresh with no projects folder lands at once (1.1b AC 13)");
    }

    [Test]
    public void AFileThatCannotBeRead_RemembersNoOne()
    {
        File.WriteAllText(Path.Combine(_directory, "project-members.json"), "{ not json");

        Assert.That(Store().Load().ByCard, Is.Empty);
    }

    [Test]
    public void TheOldListOfNames_ReadsAsNoOne_RatherThanFailing()
    {
        File.WriteAllText(Path.Combine(_directory, "project-members.json"), """["theseus"]""");

        Assert.That(Store().Load().ByCard, Is.Empty);
    }
}
