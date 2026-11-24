using Theseus.Core.CiStatus;

namespace Theseus.Tests.CiStatus;

[TestFixture]
public class CardStatusesTests
{
    private const string GitHub = "https://github.com/owner/repo.git";
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 22, 0, 0, TimeSpan.Zero);

    [Test]
    public void ACardWhoseReposHaveNoRemote_ShowsNoStatus()
    {
        var status = For([new CardStatusRepo("notes", "main", null)]);

        Assert.That(status.IsShown, Is.False);
    }

    [Test]
    public void ARepoOnAnotherHost_SaysPlainlyItIsNotRead_AndIsNeverPassing()
    {
        var status = For([new CardStatusRepo("Core", "main", "https://dev.example.com/org/Core/_git/Core")]);

        Assert.That(status.State, Is.EqualTo(CiState.NotRead));
        Assert.That(status.ButtonText, Is.EqualTo("CI not read"));
        Assert.That(status.Rows.Single().Text, Is.EqualTo("dev.example.com is not read: Theseus reads only GitHub"));
        Assert.That(status.Rows.Single().Url, Is.Null);
    }

    [Test]
    public void BeforeTheFirstRead_TheStatusIsUnknown()
    {
        var status = For([Repo("feature")]);

        Assert.That(status.State, Is.EqualTo(CiState.Unknown));
        Assert.That(status.ButtonText, Is.EqualTo("CI unknown"));
        Assert.That(status.Rows.Single().Text, Is.EqualTo("Not read from GitHub yet"));
    }

    [Test]
    public void WithTheLimitUsedUp_TheCardSaysSo_WithTheTimeItResets()
    {
        var reset = new DateTimeOffset(2026, 11, 24, 22, 13, 0, TimeSpan.Zero);
        var limited = RepoReading.Unknown(RepoReadingKind.Limited, Now) with { LimitResetsAt = reset };

        var status = For([Repo("feature")], limited);

        Assert.That(status.State, Is.EqualTo(CiState.Unknown));
        Assert.That(status.Rows.Single().Text, Is.EqualTo("GitHub's limit for calls without a token is used up until 22:13"));
    }

    [Test]
    public void ARepoNotYetRead_WhileTheLimitIsUsedUp_SaysTheLimit()
    {
        var status = CardStatuses.For([Repo("feature")], _ => null, Now.AddMinutes(13), Now, TimeZoneInfo.Utc);

        Assert.That(status.Rows.Single().Text, Is.EqualTo("GitHub's limit for calls without a token is used up until 22:13"));
    }

    [Test]
    public void ARepoGitHubDoesNotShow_SaysSo()
    {
        var status = For([Repo("feature")], RepoReading.Unknown(RepoReadingKind.NotShown, Now));

        Assert.That(status.State, Is.EqualTo(CiState.Unknown));
        Assert.That(status.Rows.Single().Text, Is.EqualTo("GitHub does not show owner/repo without a token"));
    }

    [Test]
    public void AFailedRead_IsUnknown_WithItsTime()
    {
        var status = For([Repo("feature")], RepoReading.Unknown(RepoReadingKind.Failed, Now));

        Assert.That(status.State, Is.EqualTo(CiState.Unknown));
        Assert.That(status.Rows.Single().Text, Is.EqualTo("GitHub could not be read at 22:00"));
    }

    [TestCase("detached at 1a2b3c4")]
    [TestCase(null)]
    public void ADetachedOrUnreadableBranch_IsUnknown(string? branch)
    {
        var status = For([new CardStatusRepo("repo", branch, GitHub)], Reading([Pull(41, "feature")], [Run("feature", "completed", "success")]));

        Assert.That(status.State, Is.EqualTo(CiState.Unknown));
        Assert.That(status.Rows.Single().Url, Is.Null);
    }

    [Test]
    public void AnOpenDraftPullRequest_AndARunningRun_AreShownWithLinks()
    {
        var status = For([Repo("feature")], Reading([Pull(41, "feature", draft: true)], [Run("feature", "in_progress", null)]));

        Assert.That(status.ButtonText, Is.EqualTo("PR 41 draft · CI running"));
        Assert.That(status.State, Is.EqualTo(CiState.Running));
        Assert.That(status.Rows.Select(row => row.Text), Is.EqualTo(new[] { "PR 41 open, draft", "CI running since 21:30" }));
        Assert.That(status.Rows[0].Url, Is.EqualTo("https://github.com/owner/repo/pull/41"));
        Assert.That(status.Rows[0].Title, Is.EqualTo("A change"));
        Assert.That(status.Rows[1].Url, Is.EqualTo("https://github.com/owner/repo/actions/runs/1"));
    }

    [Test]
    public void APassedRun_ShowsATick_AndItsFinishingTime()
    {
        var status = For([Repo("feature")], Reading([Pull(41, "feature")], [Run("feature", "completed", "success")]));

        Assert.That(status.ButtonText, Is.EqualTo("PR 41 · ✓ CI passed"));
        Assert.That(status.Rows[0].Text, Is.EqualTo("PR 41 open, ready"));
        Assert.That(status.Rows[1].Text, Is.EqualTo("CI passed 21:40"));
    }

    [TestCase("failure", "✗ CI failed", CiState.Failed)]
    [TestCase("timed_out", "✗ CI failed", CiState.Failed)]
    [TestCase("cancelled", "CI cancelled", CiState.Ended)]
    [TestCase("skipped", "CI skipped", CiState.Ended)]
    public void ARunThatDidNotPass_IsNeverShownAsPassing(string conclusion, string button, CiState state)
    {
        var status = For([Repo("feature")], Reading([], [Run("feature", "completed", conclusion)]));

        Assert.That(status.ButtonText, Is.EqualTo(button));
        Assert.That(status.State, Is.EqualTo(state));
    }

    [Test]
    public void TheNewestRunOnTheBranchCounts_NotAnOlderOne_NorAnotherBranchs()
    {
        var runs = new[]
        {
            Run("feature", "completed", "success", "2026-11-24T20:00:00Z", id: 1),
            Run("feature", "completed", "failure", "2026-11-24T21:00:00Z", id: 2),
            Run("main", "completed", "success", "2026-11-24T21:50:00Z", id: 3),
        };

        var status = For([Repo("feature")], Reading([], runs));

        Assert.That(status.State, Is.EqualTo(CiState.Failed));
        Assert.That(status.Rows[1].Url, Does.EndWith("/runs/2"));
    }

    [Test]
    public void NoRunOnTheBranch_SaysNoneWasFound_AndIsNotPassing()
    {
        var status = For([Repo("feature")], Reading([], [Run("main", "completed", "success")]));

        Assert.That(status.State, Is.EqualTo(CiState.NoneFound));
        Assert.That(status.ButtonText, Is.EqualTo("No CI run"));
        Assert.That(status.Rows.Select(row => row.Text), Is.EqualTo(new[]
        {
            "No open pull request from feature", "No CI run on feature among the newest 100",
        }));
    }

    [Test]
    public void APullRequestFromAFork_IsNotTheCards()
    {
        var status = For([Repo("feature")], Reading([Pull(9, "feature", headRepo: "someone/repo")], []));

        Assert.That(status.Rows[0].Text, Is.EqualTo("No open pull request from feature"));
    }

    [Test]
    public void SeveralRepos_NameEachRow_AndTheButtonShowsTheWorst_WithoutGrowing()
    {
        var repos = new[]
        {
            new CardStatusRepo("one", "feature", "https://github.com/owner/one"),
            new CardStatusRepo("two", "feature", "https://github.com/owner/two"),
            new CardStatusRepo("three", "feature", "https://dev.example.com/org/_git/three"),
        };
        var readings = new Dictionary<string, RepoReading>
        {
            ["owner/one"] = Reading([Pull(1, "feature", headRepo: "owner/one")],[Run("feature", "completed", "success")]),
            ["owner/two"] = Reading([], [Run("feature", "completed", "failure")]),
        };

        var status = CardStatuses.For(repos, repo => readings.GetValueOrDefault(repo.Key), null, Now, TimeZoneInfo.Utc);

        Assert.That(status.ButtonText, Is.EqualTo("3 repos · ✗ CI failed"));
        Assert.That(status.Rows.Select(row => row.Text), Has.Some.EqualTo("one: PR 1 open, ready"));
        Assert.That(status.Rows.Select(row => row.Text), Has.Some.EqualTo("three: dev.example.com is not read: Theseus reads only GitHub"));
    }

    [Test]
    public void AnUnknownRepo_OutranksPassedOnes()
    {
        var repos = new[]
        {
            new CardStatusRepo("one", "feature", "https://github.com/owner/one"),
            new CardStatusRepo("two", "feature", "https://github.com/owner/two"),
        };

        var status = CardStatuses.For(repos,
            repo => repo.Name == "one" ? Reading([], [Run("feature", "completed", "success")]) : null, null, Now, TimeZoneInfo.Utc);

        Assert.That(status.State, Is.EqualTo(CiState.Unknown));
        Assert.That(status.ButtonText, Is.EqualTo("2 repos · CI unknown"));
    }

    [Test]
    public void ATimeNotToday_CarriesItsDate()
    {
        Assert.That(CardStatuses.Time(Now.AddDays(-1), Now, TimeZoneInfo.Utc), Is.EqualTo("23 Nov 22:00"));
        Assert.That(CardStatuses.Time(Now.AddMinutes(-5), Now, TimeZoneInfo.Utc), Is.EqualTo("21:55"));
    }

    private static CardStatusRepo Repo(string branch) => new("repo", branch, GitHub);

    private static CardStatus For(IReadOnlyList<CardStatusRepo> repos, RepoReading? reading = null) =>
        CardStatuses.For(repos, _ => reading, null, Now, TimeZoneInfo.Utc);

    private static RepoReading Reading(IReadOnlyList<OpenPullRequest> pulls, IReadOnlyList<CiRun> runs) =>
        new(RepoReadingKind.Read, Now, pulls, runs);

    private static OpenPullRequest Pull(int number, string branch, bool draft = false, string headRepo = "owner/repo") =>
        new(number, "A change", branch, headRepo, draft, $"https://github.com/owner/repo/pull/{number}");

    private static CiRun Run(string branch, string status, string? conclusion, string created = "2026-11-24T21:30:00Z", long id = 1)
    {
        var at = DateTimeOffset.Parse(created, System.Globalization.CultureInfo.InvariantCulture);

        return new CiRun(branch, status, conclusion, $"https://github.com/owner/repo/actions/runs/{id}", at, at, at.AddMinutes(10));
    }
}
