using Theseus.App.ViewModels;
using Theseus.Core.CiStatus;

namespace Theseus.Tests.ViewModels;

[TestFixture]
public class CiStatusViewModelTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 22, 0, 0, TimeSpan.Zero);
    private static readonly CardStatusRepo Card = new("repo", "feature", "https://github.com/owner/repo.git");

    [Test]
    public async Task AClientThatThrows_LeavesTheRepoUnknown_AndThePollDoesNotThrow()
    {
        var poller = new CiStatusViewModel(new ThrowingClient(), () => Now, tick: false);
        poller.SetRepositories([new GitHubRepoRef("owner", "repo")]);

        await poller.PollAsync(refresh: true);

        Assert.That(poller.StatusFor([Card]).Rows.Single().Text, Does.StartWith("GitHub could not be read at"));
        Assert.That(poller.StatusFor([Card]).State, Is.EqualTo(CiState.Unknown));
    }

    [Test]
    public async Task ThePoll_StopsAtTheLimit_WithoutAskingForTheOtherRepos()
    {
        var client = new LimitedClient(Now.AddMinutes(30));
        var poller = new CiStatusViewModel(client, () => Now, tick: false);
        poller.SetRepositories([new GitHubRepoRef("owner", "one"), new GitHubRepoRef("owner", "two")]);

        await poller.PollAsync(refresh: true);

        Assert.That(client.Asked, Is.EqualTo(1));
        Assert.That(poller.IsCaution, Is.True);
    }

    [Test]
    public async Task OnePollReadsEachRepoOnce_HoweverManyCardsShareIt()
    {
        var client = new LimitedClient(null);
        var poller = new CiStatusViewModel(client, () => Now, tick: false);
        poller.SetRepositories([new GitHubRepoRef("owner", "repo"), new GitHubRepoRef("Owner", "Repo")]);

        await poller.PollAsync(refresh: true);

        Assert.That(client.Asked, Is.EqualTo(1));
    }

    private sealed class ThrowingClient : IGitHubStatusClient
    {
        public Task<GitHubRead> ReadAsync(GitHubRepoRef repo, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("bad charset");
    }

    private sealed class LimitedClient(DateTimeOffset? reset) : IGitHubStatusClient
    {
        public int Asked { get; private set; }

        public Task<GitHubRead> ReadAsync(GitHubRepoRef repo, CancellationToken cancellationToken)
        {
            Asked++;

            return Task.FromResult(reset is { } at
                ? new GitHubRead(RepoReading.Unknown(RepoReadingKind.Limited, Now) with { LimitResetsAt = at }, new GitHubBudget(60, 0, at))
                : new GitHubRead(new RepoReading(RepoReadingKind.Read, Now, [], []), new GitHubBudget(60, 50, Now.AddMinutes(40))));
        }
    }
}
