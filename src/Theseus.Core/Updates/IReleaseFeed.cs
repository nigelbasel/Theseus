namespace Theseus.Core.Updates;

/// <summary>Where Theseus learns about published builds.</summary>
public interface IReleaseFeed
{
    /// <summary>The newest published release, or null when there is none or it cannot be read.</summary>
    Task<AvailableRelease?> LatestAsync(CancellationToken cancellationToken);

    /// <summary>Downloads one of a release's files.</summary>
    Task<byte[]> DownloadAsync(Uri uri, CancellationToken cancellationToken);
}
