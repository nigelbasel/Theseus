using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

/// <summary>
/// Pull-request links for a card whose repos have more than one origin are not guessed (card-branches spec AC 11).
/// </summary>
[TestFixture]
public class PullRequestResolverRepoTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-pr-repos-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public void ReposWithOneOriginBetweenThem_LinkAsBefore()
    {
        var api = Clone("api");
        var ui = Clone("ui");
        var remotes = new Remotes { [api] = "https://github.com/owner/app.git", [ui] = "https://github.com/owner/app.git" };

        var links = new PullRequestResolver(remotes).Resolve(Card([api, ui], "Review PR 42."));

        Assert.That(links.Single().Url, Is.EqualTo("https://github.com/owner/app/pull/42"));
    }

    [Test]
    public void ReposWithSeveralOrigins_GiveNoLink_AndSayWhichReposItCouldBeIn()
    {
        var api = Clone("api");
        var ui = Clone("ui");
        var remotes = new Remotes { [api] = "https://github.com/owner/api.git", [ui] = "https://github.com/owner/ui.git" };

        var link = new PullRequestResolver(remotes).Resolve(Card([api, ui], "Review PR 42.")).Single();

        Assert.That(link.Url, Is.Null, "a link to the wrong repository is worse than none");
        Assert.That(link.Note, Is.EqualTo("in one of api, ui"));
    }

    [Test]
    public void ACardThatDeclaresNoRepos_ResolvesAsBefore()
    {
        var working = Clone("notes");
        var remotes = new Remotes { [working] = "https://github.com/owner/notes.git" };

        var links = new PullRequestResolver(remotes).Resolve(Card([], "Review PR 42.", working));

        Assert.That(links.Single().Url, Is.EqualTo("https://github.com/owner/notes/pull/42"));
        Assert.That(links.Single().Note, Is.Null);
    }

    private string Clone(string name)
    {
        var path = Directory.CreateDirectory(Path.Combine(_root, name)).FullName;
        Directory.CreateDirectory(Path.Combine(path, ".git"));

        return path;
    }

    private Workstream Card(IReadOnlyList<string> addDirectories, string prompt, string? working = null) =>
        new("feature", "Feature", "#208DD7", false, working ?? _root, addDirectories, prompt, 0, "New", false, false,
            false, null, [], null, DateTimeOffset.UnixEpoch);

    private sealed class Remotes : Dictionary<string, string>, IGitRemoteReader
    {
        public Remotes()
            : base(StringComparer.OrdinalIgnoreCase)
        {
        }

        public string? ReadOriginUrl(string? directory) =>
            directory is not null && TryGetValue(directory, out var url) ? url : null;
    }
}
