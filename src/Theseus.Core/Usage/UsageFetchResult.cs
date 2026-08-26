namespace Theseus.Core.Usage;

/// <summary>The outcome of one attempt to read usage.</summary>
/// <param name="Usage">What was read, or no reading.</param>
/// <param name="WasThrottled">
/// Whether the service asked us to back off. The usage endpoint rate-limits hard, and treating a
/// refusal as an ordinary failure turns one throttle into a storm of them.
/// </param>
/// <param name="Problem">A short description of what went wrong, for the status bar.</param>
public sealed record UsageFetchResult(AccountUsage Usage, bool WasThrottled, string? Problem)
{
    /// <summary>A successful read.</summary>
    public static UsageFetchResult Ok(AccountUsage usage) => new(usage, false, null);

    /// <summary>The service asked us to back off.</summary>
    public static UsageFetchResult Throttled() => new(AccountUsage.None, true, "rate limited");

    /// <summary>The read failed for some other reason.</summary>
    public static UsageFetchResult Failed(string problem) => new(AccountUsage.None, false, problem);
}
