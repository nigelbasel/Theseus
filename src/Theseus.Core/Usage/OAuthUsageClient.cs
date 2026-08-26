using System.Net;
using System.Net.Http.Headers;
using Theseus.Core.Abstractions;
using Theseus.Core.Diagnostics;

namespace Theseus.Core.Usage;

/// <summary>
/// Reads the account's usage from the endpoint Claude Code's own usage view uses.
///
/// The endpoint is undocumented and rate-limits aggressively. Two things keep it usable: the
/// <c>claude-code</c> user agent, without which requests land in a punitive bucket, and a floor on
/// how often it is called. Neither is optional.
/// </summary>
public sealed class OAuthUsageClient : IUsageClient, IDisposable
{
    private const string Endpoint = "https://api.anthropic.com/api/oauth/usage";

    private readonly HttpClient _http;
    private readonly ICredentialSource _credentials;
    private readonly ISystemClock _clock;

    /// <summary>Creates the client.</summary>
    /// <param name="credentials">Where the bearer token comes from, read fresh on every call.</param>
    /// <param name="clock">Used only to decide whether a token has expired.</param>
    /// <param name="claudeCodeVersion">The version to present as, which the endpoint requires.</param>
    public OAuthUsageClient(ICredentialSource credentials, ISystemClock clock, string claudeCodeVersion)
    {
        _credentials = credentials;
        _clock = clock;

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _http.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("claude-code", claudeCodeVersion));
        _http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <inheritdoc />
    public async Task<UsageFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        if (_credentials.Read() is not { } credentials)
        {
            return UsageFetchResult.Failed("not signed in");
        }

        if (!credentials.IsUsable(_clock.UtcNow))
        {
            return UsageFetchResult.Failed("session expired");
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);

        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                return UsageFetchResult.Throttled();
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                return UsageFetchResult.Failed("not authorised");
            }

            if (!response.IsSuccessStatusCode)
            {
                return UsageFetchResult.Failed($"usage unavailable ({(int)response.StatusCode})");
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var usage = UsageResponseParser.Parse(body, _clock.UtcNow);

            return usage.HasReading
                ? UsageFetchResult.Ok(usage)
                : UsageFetchResult.Failed("usage not reported");
        }
        catch (TaskCanceledException)
        {
            TheseusLog.Debug("The usage request timed out");

            return UsageFetchResult.Failed("usage timed out");
        }
        catch (HttpRequestException exception)
        {
            TheseusLog.Debug("The usage request could not be sent: {Reason}", exception.Message);

            return UsageFetchResult.Failed("offline");
        }
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();
}
