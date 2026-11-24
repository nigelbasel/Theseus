namespace Theseus.Core.Updates;

/// <summary>Where Theseus learns about published builds.</summary>
public interface IReleaseFeed
{
    /// <summary>The newest published release, or null when there is none or it cannot be read.</summary>
    Task<AvailableRelease?> LatestAsync(CancellationToken cancellationToken);

    /// <summary>Downloads one of a release's files.</summary>
    Task<byte[]> DownloadAsync(Uri uri, CancellationToken cancellationToken);

    /// <summary>
    /// Downloads one of a release's files, saying how far it has got: a fraction from 0 to 1, or null while its size is
    /// not known (baseline W52, G148). A feed that cannot tell reports nothing.
    /// </summary>
    Task<byte[]> DownloadAsync(Uri uri, IProgress<double?>? progress, CancellationToken cancellationToken) =>
        DownloadAsync(uri, cancellationToken);
}
