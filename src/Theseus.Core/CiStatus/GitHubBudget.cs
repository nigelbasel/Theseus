using System.Globalization;
using System.Net.Http.Headers;

namespace Theseus.Core.CiStatus;

/// <summary>
/// GitHub's limit for calls without a token, as its last answer reported it: 60 an hour per address, and a 304 costs
/// one too (measured 2026-11-19 and 2026-11-24, pr-ci-status research).
/// </summary>
/// <param name="Limit">Calls allowed in the window.</param>
/// <param name="Remaining">Calls left in it.</param>
/// <param name="ResetsAt">When the window starts again.</param>
public sealed record GitHubBudget(int Limit, int Remaining, DateTimeOffset ResetsAt)
{
    /// <summary>The budget a response's headers report, or null when they report none.</summary>
    public static GitHubBudget? FromHeaders(HttpResponseHeaders headers)
    {
        ArgumentNullException.ThrowIfNull(headers);

        if (Number(headers, "X-RateLimit-Limit") is not { } limit
            || Number(headers, "X-RateLimit-Remaining") is not { } remaining
            || Number(headers, "X-RateLimit-Reset") is not { } reset)
        {
            return null;
        }

        return new GitHubBudget((int)limit, (int)remaining, DateTimeOffset.FromUnixTimeSeconds(reset));
    }

    private static long? Number(HttpResponseHeaders headers, string name) =>
        headers.TryGetValues(name, out var values)
        && long.TryParse(values.FirstOrDefault(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number
            : null;
}
