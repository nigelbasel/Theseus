using System.Net;
using System.Net.Http;
using Theseus.Core.CiStatus;

namespace Theseus.Tests.CiStatus;

[TestFixture]
public class GitHubStatusClientTests
{
    private static readonly GitHubRepoRef Repo = new("owner", "repo");
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 22, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task Read_AsksForTheOpenPullRequestsThenTheNewestRuns_WithNoCredential()
    {
        var handler = new ScriptedHandler(_ => Ok(GitHubJson.Pulls()), _ => Ok(GitHubJson.Runs()));
        using var client = Client(handler);

        var read = await client.ReadAsync(Repo, CancellationToken.None);

        Assert.That(read.Reading.Kind, Is.EqualTo(RepoReadingKind.Read));
        Assert.That(handler.Sent.Select(request => request.RequestUri!.AbsoluteUri), Is.EqualTo(new[]
        {
            "https://api.github.com/repos/owner/repo/pulls?state=open&per_page=100",
            "https://api.github.com/repos/owner/repo/actions/runs?per_page=100",
        }));
        Assert.That(handler.Sent.All(request => request.Method == HttpMethod.Get), Is.True);
        Assert.That(handler.Sent.Any(request => request.Headers.Authorization is not null || request.Headers.Contains("Authorization")),
            Is.False);
    }

    [Test]
    public async Task ASecondRead_IsConditional_AndA304ReusesTheListAlreadyRead()
    {
        var handler = new ScriptedHandler(
            _ => Ok(GitHubJson.Pulls(GitHubJson.Pull(41, "feature")), "\"p1\""),
            _ => Ok(GitHubJson.Runs(GitHubJson.Run(1, "feature", "completed", "success", "2026-11-24T21:00:00Z")), "\"r1\""),
            _ => new HttpResponseMessage(HttpStatusCode.NotModified),
            _ => new HttpResponseMessage(HttpStatusCode.NotModified));
        using var client = Client(handler);

        await client.ReadAsync(Repo, CancellationToken.None);
        var again = await client.ReadAsync(Repo, CancellationToken.None);

        Assert.That(handler.Sent[2].Headers.IfNoneMatch.Single().Tag, Is.EqualTo("\"p1\""));
        Assert.That(handler.Sent[3].Headers.IfNoneMatch.Single().Tag, Is.EqualTo("\"r1\""));
        Assert.That(again.Reading.Kind, Is.EqualTo(RepoReadingKind.Read));
        Assert.That(again.Reading.PullRequests.Single().Number, Is.EqualTo(41));
        Assert.That(again.Reading.Runs.Single().Conclusion, Is.EqualTo("success"));
    }

    [Test]
    public async Task TheBudget_IsTakenFromTheHeaders()
    {
        var handler = new ScriptedHandler(_ => Ok(GitHubJson.Pulls(), remaining: 41), _ => Ok(GitHubJson.Runs(), remaining: 40));
        using var client = Client(handler);

        var read = await client.ReadAsync(Repo, CancellationToken.None);

        Assert.That(read.Budget, Is.EqualTo(new GitHubBudget(60, 40, DateTimeOffset.FromUnixTimeSeconds(1795561990))));
    }

    [TestCase(HttpStatusCode.Forbidden)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    public async Task ALimitUsedUp_IsLimited_WithItsReset_AndMakesNoSecondCall(HttpStatusCode status)
    {
        var handler = new ScriptedHandler(_ => WithBudget(new HttpResponseMessage(status), 0));
        using var client = Client(handler);

        var read = await client.ReadAsync(Repo, CancellationToken.None);

        Assert.That(read.Reading.Kind, Is.EqualTo(RepoReadingKind.Limited));
        Assert.That(read.Reading.LimitResetsAt, Is.EqualTo(DateTimeOffset.FromUnixTimeSeconds(1795561990)));
        Assert.That(handler.Sent, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task ATooManyRequestsWithoutHeaders_WaitsAMinute()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        using var client = Client(handler);

        var read = await client.ReadAsync(Repo, CancellationToken.None);

        Assert.That(read.Reading.Kind, Is.EqualTo(RepoReadingKind.Limited));
        Assert.That(read.Reading.LimitResetsAt, Is.EqualTo(Now.AddMinutes(1)));
    }

    [Test]
    public async Task ASecondaryLimit_WaitsAsLongAsRetryAfterSays()
    {
        var handler = new ScriptedHandler(_ =>
        {
            var response = WithBudget(new HttpResponseMessage(HttpStatusCode.Forbidden), 30);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(90));

            return response;
        });
        using var client = Client(handler);

        var read = await client.ReadAsync(Repo, CancellationToken.None);

        Assert.That(read.Reading.Kind, Is.EqualTo(RepoReadingKind.Limited));
        Assert.That(read.Reading.LimitResetsAt, Is.EqualTo(Now.AddSeconds(90)));
    }

    [Test]
    public async Task AForbiddenWithCallsLeft_IsAFailure_NotTheLimit()
    {
        var handler = new ScriptedHandler(_ => WithBudget(new HttpResponseMessage(HttpStatusCode.Forbidden), 30));
        using var client = Client(handler);

        var read = await client.ReadAsync(Repo, CancellationToken.None);

        Assert.That(read.Reading.Kind, Is.EqualTo(RepoReadingKind.Failed));
    }

    [Test]
    public async Task NotFound_IsNotShown()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var client = Client(handler);

        var read = await client.ReadAsync(Repo, CancellationToken.None);

        Assert.That(read.Reading.Kind, Is.EqualTo(RepoReadingKind.NotShown));
    }

    [Test]
    public async Task AFailureOnTheRunList_IsAFailure_NotAHalfReading()
    {
        var handler = new ScriptedHandler(_ => Ok(GitHubJson.Pulls()), _ => new HttpResponseMessage(HttpStatusCode.BadGateway));
        using var client = Client(handler);

        var read = await client.ReadAsync(Repo, CancellationToken.None);

        Assert.That(read.Reading.Kind, Is.EqualTo(RepoReadingKind.Failed));
        Assert.That(read.Reading.PullRequests, Is.Empty);
    }

    [Test]
    public async Task AnUnreachableGitHub_IsAFailure()
    {
        var handler = new ScriptedHandler(_ => throw new HttpRequestException("no route"));
        using var client = Client(handler);

        var read = await client.ReadAsync(Repo, CancellationToken.None);

        Assert.That(read.Reading.Kind, Is.EqualTo(RepoReadingKind.Failed));
        Assert.That(read.Reading.At, Is.EqualTo(Now));
    }

    private static GitHubStatusClient Client(ScriptedHandler handler) => new("1.0", handler, () => Now);

    private static HttpResponseMessage Ok(string body, string? etag = null, int remaining = 50)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };

        if (etag is not null)
        {
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag);
        }

        return WithBudget(response, remaining);
    }

    private static HttpResponseMessage WithBudget(HttpResponseMessage response, int remaining)
    {
        response.Headers.Add("X-RateLimit-Limit", "60");
        response.Headers.Add("X-RateLimit-Remaining", remaining.ToString(System.Globalization.CultureInfo.InvariantCulture));
        response.Headers.Add("X-RateLimit-Reset", "1795561990");

        return response;
    }
}
