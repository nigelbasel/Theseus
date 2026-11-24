using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.CiStatus;

/// <summary>
/// Reads a repository's open pull requests and newest workflow runs from GitHub, with no token and nothing written:
/// the criterion approved 2026-11-19 forbids a token on the machine, and Theseus only reads trackers. Two calls per
/// repository. Each is conditional on the last ETag: without a token a 304 still costs a call (measured 2026-11-19),
/// but it does not send the 1.5 MB run list again (measured 2026-11-24).
/// </summary>
public sealed class GitHubStatusClient : IGitHubStatusClient, IDisposable
{
    /// <summary>How many runs one call asks for, the most GitHub gives in one page.</summary>
    public const int RunsPerRead = 100;

    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, (string ETag, string Body)> _cache = new(StringComparer.Ordinal);
    private readonly Func<DateTimeOffset> _now;

    /// <summary>Creates the client, presenting itself as this build of Theseus.</summary>
    public GitHubStatusClient(string currentVersion)
        : this(currentVersion, new HttpClientHandler(), () => DateTimeOffset.UtcNow)
    {
    }

    /// <summary>The same client over a given handler and clock, so a test can see each request.</summary>
    public GitHubStatusClient(string currentVersion, HttpMessageHandler handler, Func<DateTimeOffset> now)
    {
        _now = now;
        _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Theseus",
            string.IsNullOrWhiteSpace(currentVersion) ? "unknown" : currentVersion));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    /// <summary>The open pull request list's address.</summary>
    public static Uri PullRequestsAddress(GitHubRepoRef repo) =>
        new($"https://api.github.com/repos/{repo.FullName}/pulls?state=open&per_page=100");

    /// <summary>The run list's address.</summary>
    public static Uri RunsAddress(GitHubRepoRef repo) =>
        new($"https://api.github.com/repos/{repo.FullName}/actions/runs?per_page={RunsPerRead}");

    /// <inheritdoc />
    public async Task<GitHubRead> ReadAsync(GitHubRepoRef repo, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repo);

        var pulls = await GetAsync(PullRequestsAddress(repo), cancellationToken);

        if (pulls.Body is null)
        {
            return new GitHubRead(pulls.Failure!, pulls.Budget);
        }

        var runs = await GetAsync(RunsAddress(repo), cancellationToken);

        if (runs.Body is null)
        {
            return new GitHubRead(runs.Failure!, runs.Budget ?? pulls.Budget);
        }

        try
        {
            return new GitHubRead(new RepoReading(RepoReadingKind.Read, _now(), GitHubLists.PullRequests(pulls.Body),
                GitHubLists.Runs(runs.Body)), runs.Budget ?? pulls.Budget);
        }
        catch (JsonException exception)
        {
            TheseusLog.Information("GitHub's answer for {Repo} could not be read: {Reason}", repo.FullName, exception.Message);

            return new GitHubRead(RepoReading.Unknown(RepoReadingKind.Failed, _now()), runs.Budget ?? pulls.Budget);
        }
    }

    private async Task<(string? Body, RepoReading? Failure, GitHubBudget? Budget)> GetAsync(Uri address,
        CancellationToken cancellationToken)
    {
        var key = address.AbsoluteUri;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, address);

            if (_cache.TryGetValue(key, out var cached))
            {
                request.Headers.TryAddWithoutValidation("If-None-Match", cached.ETag);
            }

            using var response = await _http.SendAsync(request, cancellationToken);
            var budget = GitHubBudget.FromHeaders(response.Headers);

            if (response.StatusCode == HttpStatusCode.NotModified && _cache.TryGetValue(key, out var unchanged))
            {
                return (unchanged.Body, null, budget);
            }

            if (response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.Headers.ETag is { } etag)
                {
                    _cache[key] = (etag.ToString(), body);
                }

                return (body, null, budget);
            }

            if (response.StatusCode == HttpStatusCode.TooManyRequests
                || (response.StatusCode == HttpStatusCode.Forbidden && budget is { Remaining: 0 }))
            {
                TheseusLog.Information("GitHub's limit for calls without a token is used up until {Reset}", budget?.ResetsAt);

                return (null, RepoReading.Unknown(RepoReadingKind.Limited, _now()) with { LimitResetsAt = budget?.ResetsAt },
                    budget);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return (null, RepoReading.Unknown(RepoReadingKind.NotShown, _now()), budget);
            }

            TheseusLog.Information("GitHub answered {Status} for {Address}", (int)response.StatusCode, address);

            return (null, RepoReading.Unknown(RepoReadingKind.Failed, _now()), budget);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            TheseusLog.Information("GitHub could not be reached for {Address}: {Reason}", address, exception.Message);

            return (null, RepoReading.Unknown(RepoReadingKind.Failed, _now()), null);
        }
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();
}
