namespace Theseus.Core.Usage;

/// <summary>
/// One rolling window the account is measured against, as the usage endpoint reports it.
/// </summary>
/// <param name="Kind">The window's identifier, for example <c>session</c> or <c>weekly_all</c>.</param>
/// <param name="Group">Which family it belongs to, for example <c>weekly</c>.</param>
/// <param name="Percent">How much of the window has been consumed.</param>
/// <param name="Severity">How close to the limit the service considers this, for example <c>warning</c>.</param>
/// <param name="ResetsAtUtc">When the window rolls over, if it has a fixed reset.</param>
/// <param name="ScopeLabel">What the window is scoped to, such as a model name, when it is scoped.</param>
/// <param name="IsActive">Whether this is the window currently constraining the account.</param>
public sealed record UsageLimit(
    string Kind,
    string Group,
    double Percent,
    string Severity,
    DateTimeOffset? ResetsAtUtc,
    string? ScopeLabel,
    bool IsActive);
