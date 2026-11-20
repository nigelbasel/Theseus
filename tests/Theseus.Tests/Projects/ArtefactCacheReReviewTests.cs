using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectCatalogTests;

namespace Theseus.Tests.Projects;

/// <summary>
/// The artefact cache after the re-review of PR 2: a tag named like a branch does not hide the branch's tip (item 2);
/// the cache keeps one entry per branch however many commits pass (item 3); files are read at the commit the source
/// names (item 4).
/// </summary>
[TestFixture]
public class ArtefactCacheReReviewTests
{
    private const string Primary = "owner/project";

    private const string Body = "## Phase 0: Seed\n\n### Objective 0.1: First\n- [ ] One.\n\n**Pipeline:** feature\n"
                                + "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n"
                                + "| A | a-slug | owner/a-slug | Repo |\n";

    private TestRepository _repository = null!;
    private ProjectFile _project = null!;

    [SetUp]
    public void SetUp()
    {
        _repository = TestRepository.Create();
        _repository.Commit("projects/a-project.md", ProjectText("A project", primary: Primary, body: Body));
        _repository.Branch(Primary);
        _repository.Branch("owner/a-slug");
        _repository.Commit("work/a-slug/spec.md", "# A spec\n");
        _repository.Checkout(Primary);
        _project = ProjectFileReader.Read("a-project", ProjectText("A project", primary: Primary, body: Body)).File!;
    }

    [TearDown]
    public void TearDown() => _repository.Dispose();

    private GitRepositoryReader Repository() => new(new GitCli(), _repository.Folder);

    [Test]
    public void TagsNamedLikeThePrimaryAndTheWorkstreamBranch_DoNotHideTheirTips()
    {
        _repository.Git("tag", Primary, "main");
        _repository.Git("tag", "owner/a-slug", "main");

        var artefacts = new ArtefactCache().Read(Repository(), _project)["a-slug"];

        Assert.That(Repository().BranchTips().Keys, Is.SupersetOf(new[] { Primary, "owner/a-slug" }));
        Assert.That((artefacts.Source.Branch, artefacts.Source.IsMerged), Is.EqualTo(("owner/a-slug", false)),
            "neither lost (not on this machine) nor taken for merged");
        Assert.That(artefacts.SpecText, Is.EqualTo("# A spec\n"));
    }

    [Test]
    public void TheCache_KeepsOneEntryPerBranch_HoweverManyCommitsPass()
    {
        var cache = new ArtefactCache();
        cache.Read(Repository(), _project);
        var after = cache.EntryCount;

        for (var change = 0; change < 3; change++)
        {
            _repository.Checkout("owner/a-slug");
            _repository.Commit("work/a-slug/spec.md", $"# A spec, change {change}\n");
            _repository.Checkout(Primary);
            _repository.Commit("notes.txt", $"Primary change {change}\n");
            cache.Read(Repository(), _project);
        }

        Assert.That(cache.EntryCount, Is.EqualTo(after));
    }

    [Test]
    public void FilesAreRead_AtTheCommitTheSourceNames_NotTheBranchsTipNow()
    {
        var repository = Repository();
        var snapshot = repository.Commit("owner/a-slug")!;
        _repository.Checkout("owner/a-slug");
        _repository.Commit("work/a-slug/spec.md", "# A spec, moved on since\n");
        _repository.Checkout(Primary);

        var artefacts = WorkstreamArtefactReader.ReadFrom(repository, "a-slug", new WorkstreamSource("owner/a-slug", snapshot, false), null);

        Assert.That(artefacts.SpecText, Is.EqualTo("# A spec\n"));
        Assert.That(artefacts.Source.Commit, Is.EqualTo(snapshot));
    }
}
