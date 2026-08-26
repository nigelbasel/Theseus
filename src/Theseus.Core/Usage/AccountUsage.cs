namespace Theseus.Core.Usage;

/// <summary>What the account's usage looked like at one moment.</summary>
/// <param name="Limits">Every window the service reported.</param>
/// <param name="RetrievedUtc">When it was read, so a stale reading can say so.</param>
public sealed record AccountUsage(IReadOnlyList<UsageLimit> Limits, DateTimeOffset RetrievedUtc)
{
    /// <summary>A reading that has never succeeded.</summary>
    public static AccountUsage None { get; } = new([], DateTimeOffset.MinValue);

    /// <summary>The five-hour window, which is the one that bites during a working day.</summary>
    public UsageLimit? Session => Limits.FirstOrDefault(limit =>
        string.Equals(limit.Kind, "session", StringComparison.OrdinalIgnoreCase));

    /// <summary>The seven-day window across everything.</summary>
    public UsageLimit? Weekly => Limits.FirstOrDefault(limit =>
        string.Equals(limit.Kind, "weekly_all", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The window actually constraining the account right now. A model-scoped weekly limit can be
    /// the binding one while the headline weekly figure still looks comfortable.
    /// </summary>
    public UsageLimit? Binding =>
        Limits.FirstOrDefault(limit => limit.IsActive)
        ?? Limits.OrderByDescending(limit => limit.Percent).FirstOrDefault();

    /// <summary>Whether any reading has been taken.</summary>
    public bool HasReading => Limits.Count > 0;
}
