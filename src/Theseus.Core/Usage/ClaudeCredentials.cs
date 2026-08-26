namespace Theseus.Core.Usage;

/// <summary>
/// The OAuth credentials Claude Code holds for the account. Read at the moment they are needed and
/// never written anywhere by this application.
/// </summary>
/// <param name="AccessToken">The bearer token.</param>
/// <param name="ExpiresAtUtc">When the bearer token stops working.</param>
/// <param name="SubscriptionType">The plan, for example <c>max</c>.</param>
public sealed record ClaudeCredentials(
    string AccessToken,
    DateTimeOffset ExpiresAtUtc,
    string? SubscriptionType)
{
    /// <summary>Whether the token is still usable.</summary>
    public bool IsUsable(DateTimeOffset now) =>
        !string.IsNullOrWhiteSpace(AccessToken) && ExpiresAtUtc > now;
}
