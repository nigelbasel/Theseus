namespace Theseus.Core.Usage;

/// <summary>
/// Which Claude account Claude Code is signed in as. Identity only: no token, no account or
/// organisation identifier, nothing that could be used to act as the account.
/// </summary>
/// <param name="EmailAddress">The signed-in address.</param>
/// <param name="OrganizationName">The organisation the account belongs to.</param>
/// <param name="SubscriptionType">The plan, for example <c>max</c>.</param>
public sealed record ClaudeAccount(string? EmailAddress, string? OrganizationName, string? SubscriptionType)
{
    /// <summary>Whether anything was found to show.</summary>
    public bool IsKnown => !string.IsNullOrWhiteSpace(EmailAddress);

    /// <summary>The account on one line, for the status bar.</summary>
    public string Describe => string.Join(" · ", new[] { EmailAddress, SubscriptionType }
        .Where(part => !string.IsNullOrWhiteSpace(part)));
}
