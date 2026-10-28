using Theseus.Core.Diagnostics;

namespace Theseus.Core.Updates;

/// <summary>
/// Fetches a release's installer and checks it against the release's SHA256SUMS.txt before anything
/// runs it. An installer that cannot be checked is refused rather than trusted: the MSI runs
/// elevated, so a damaged or substituted file is the one thing this must not hand to msiexec.
/// </summary>
public static class UpdateDownloader
{
    /// <summary>Downloads and verifies the installer into a folder, returning its path.</summary>
    /// <exception cref="InvalidDataException">The release has no checksum for it, or the file does not match.</exception>
    public static async Task<string> FetchAsync(IReleaseFeed feed, AvailableRelease release, string directory,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(feed);
        ArgumentNullException.ThrowIfNull(release);

        if (release.Checksums is null)
        {
            throw new InvalidDataException($"Release {release.Version} carries no {GitHubReleaseFeed.ChecksumsAsset}, so its installer cannot be checked.");
        }

        var sums = System.Text.Encoding.ASCII.GetString(await feed.DownloadAsync(release.Checksums, cancellationToken));

        if (Sha256Sums.HashFor(sums, GitHubReleaseFeed.InstallerAsset) is not { } expected)
        {
            throw new InvalidDataException($"{GitHubReleaseFeed.ChecksumsAsset} for {release.Version} lists no {GitHubReleaseFeed.InstallerAsset}.");
        }

        var installer = await feed.DownloadAsync(release.Installer, cancellationToken);

        if (!Sha256Sums.Matches(installer, expected))
        {
            throw new InvalidDataException($"The downloaded installer for {release.Version} does not match its published SHA-256.");
        }

        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"Theseus-{release.Version}.msi");
        await File.WriteAllBytesAsync(path, installer, cancellationToken);

        TheseusLog.Information("Downloaded and verified Theseus {Version} to {Path} (SHA-256 {Hash})",
            release.Version, path, expected);

        return path;
    }
}
