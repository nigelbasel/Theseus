namespace Theseus.Core.Usage;

/// <summary>Fetches the account's current usage.</summary>
public interface IUsageClient
{
    /// <summary>Reads usage now. Returns no reading rather than throwing on any failure.</summary>
    Task<UsageFetchResult> FetchAsync(CancellationToken cancellationToken);
}
