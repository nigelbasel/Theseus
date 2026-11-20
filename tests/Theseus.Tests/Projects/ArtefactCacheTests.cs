using Theseus.Core.Projects;
using static Theseus.Tests.Projects.ProjectCatalogTests;

namespace Theseus.Tests.Projects;

/// <summary>
/// A slug's files are read again only when what they are read from has moved (PR 2 review, open cost): unchanged tips
/// run one git command in all; a moved branch re-reads its slug alone; a merge moves the slug to the primary with its
/// spec's identity unchanged; a primary that moves leaves an unmerged slug's files alone.
/// </summary>
[TestFixture]
public class ArtefactCacheTests
{
    private const string Primary = "owner/project";

    private const string Body = "## Phase 0: Seed\n\n### Objective 0.1: First\n- [ ] One.\n\n**Pipeline:** feature\n"
                                + "| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n"
                                + "| A | a-slug | owner/a-slug | Repo |\n| B | b-slug | owner/b-slug | Repo |\n";

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
        _repository.Branch("owner/b-slug");
        _repository.Commit("work/b-slug/research.md", "# B research\n");
        _repository.Checkout(Primary);
        _project = ProjectFileReader.Read("a-project", ProjectText("A project", primary: Primary, body: Body)).File!;
    }

    [TearDown]
    public void TearDown() => _repository.Dispose();

    private (IReadOnlyDictionary<string, WorkstreamArtefacts> Artefacts, List<IReadOnlyList<string>> Commands) Read(ArtefactCache cache)
    {
        var git = new RecordingGit(new GitCli());

        return (cache.Read(new GitRepositoryReader(git, _repository.Folder), _project), git.Commands);
    }

    private static bool Touches(IEnumerable<IReadOnlyList<string>> commands, string slug) =>
        commands.Any(command => command.Any(argument => argument.Contains($"work/{slug}", StringComparison.Ordinal)));

    [Test]
    public void WithNoTipMoved_ASecondReadRunsOnlyTheOneCommandForTheTips()
    {
        var cache = new ArtefactCache();
        var first = Read(cache);

        var second = Read(cache);

        Assert.That(second.Commands.Select(command => command[0]), Is.EqualTo(new[] { "for-each-ref" }));
        Assert.That(second.Artefacts["a-slug"].SpecHash, Is.EqualTo(first.Artefacts["a-slug"].SpecHash).And.Not.Null);
    }

    [Test]
    public void TheCachedArtefacts_AreWhatTheUncachedReaderReads()
    {
        var cached = Read(new ArtefactCache()).Artefacts;
        var repository = new GitRepositoryReader(new GitCli(), _repository.Folder);

        foreach (var row in _project.Workstreams)
        {
            var direct = WorkstreamArtefactReader.Read(repository, _project, row, WorkstreamArtefactReader.KitMoveParent(repository, _project));

            Assert.That((cached[row.Slug].Source, cached[row.Slug].SpecHash, cached[row.Slug].Files.Order().ToList()),
                Is.EqualTo((direct.Source, direct.SpecHash, direct.Files.Order().ToList())), row.Slug);
        }
    }

    [Test]
    public void AMovedBranchTip_RereadsThatSlugAlone()
    {
        var cache = new ArtefactCache();
        var before = Read(cache).Artefacts["a-slug"].SpecHash;
        _repository.Checkout("owner/a-slug");
        _repository.Commit("work/a-slug/spec.md", "# A spec, changed\n");
        _repository.Checkout(Primary);

        var (artefacts, commands) = Read(cache);

        Assert.That(Touches(commands, "a-slug"), Is.True);
        Assert.That(Touches(commands, "b-slug"), Is.False, "b-slug's branch did not move");
        Assert.That(artefacts["a-slug"].SpecHash, Is.Not.EqualTo(before));
    }

    [Test]
    public void AMerge_MovesTheSlugToThePrimary_KeepingItsSpecsIdentity()
    {
        var cache = new ArtefactCache();
        var before = Read(cache).Artefacts["a-slug"];
        var merge = _repository.Merge("owner/a-slug");

        var after = Read(cache).Artefacts["a-slug"];

        Assert.That(after.Source, Is.EqualTo(new WorkstreamSource(Primary, merge, true)));
        Assert.That(after.SpecHash, Is.EqualTo(before.SpecHash), "an acceptance given on the branch still holds");
    }

    [Test]
    public void OnlyThePrimaryMoving_LeavesAnUnmergedSlugsFilesAlone()
    {
        var cache = new ArtefactCache();
        Read(cache);
        _repository.Commit("notes.txt", "Something else on the primary.\n");

        var (artefacts, commands) = Read(cache);

        Assert.That(artefacts["a-slug"].Source.Branch, Is.EqualTo("owner/a-slug"));
        Assert.That(Touches(commands, "a-slug"), Is.False);
        Assert.That(commands.Select(command => command[0]), Does.Contain("merge-base"), "whether it is merged is worked out again");
    }
}
