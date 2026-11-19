using Theseus.App.ViewModels;
using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

/// <summary>
/// card-branches review, should-fix 1 and 2 (2026-11-19): a card whose declared repos have no readable origin does
/// not fall back to guessing from its working directory, and a PR row's changed reason reaches the card.
/// </summary>
[TestFixture]
public class CardBranchesReviewFixTests
{
    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"theseus-card-review-{Guid.NewGuid():N}");
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
    public void DeclaredReposWithNoOrigin_GiveNoLink_EvenWhenTheWorkingDirectoryHasOne()
    {
        var working = Clone("notes");
        var api = Clone("api");
        var ui = Clone("ui");
        var remotes = new Remotes { [working] = "https://github.com/owner/notes.git" };

        var link = new PullRequestResolver(remotes).Resolve(Card(working, [api, ui], "Review PR 42.")).Single();

        Assert.That(link.Url, Is.Null, "the working directory is not one of the card's repos (AC 4)");
        Assert.That(link.Note, Is.EqualTo("api, ui have no remote to link to"));
    }

    [Test]
    public void APullRequestRowWhoseReasonChanged_IsReplacedOnTheCard()
    {
        var card = new WorkstreamCardViewModel(Card(_root, [], "Review PR 42."));
        card.SetItems([], [new CardItemRow("PR 42", null, null, whyNoLink: "in one of api, ui")]);

        card.SetItems([], [new CardItemRow("PR 42", null, null, whyNoLink: "in one of api, db")]);

        Assert.That(card.PullRequestItems.Single().Tooltip, Does.Contain("in one of api, db"));
    }

    private string Clone(string name)
    {
        var path = Directory.CreateDirectory(Path.Combine(_root, name)).FullName;
        Directory.CreateDirectory(Path.Combine(path, ".git"));

        return path;
    }

    private static Workstream Card(string working, IReadOnlyList<string> addDirectories, string prompt) =>
        new("feature", "Feature", "#208DD7", false, working, addDirectories, prompt, 0, "New", false, false,
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
