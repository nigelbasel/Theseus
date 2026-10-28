using System.Security.Cryptography;
using System.Text;
using Theseus.Core.Updates;

namespace Theseus.Tests.Updates;

[TestFixture]
public class UpdateDownloaderTests
{
    private static readonly Uri Msi = new("https://example.com/v2026.10.29.100/Theseus.msi");
    private static readonly Uri Sums = new("https://example.com/v2026.10.29.100/SHA256SUMS.txt");
    private static readonly byte[] Installer = Encoding.ASCII.GetBytes("pretend msi");

    private string _directory = string.Empty;

    [SetUp]
    public void SetUp() => _directory = Path.Combine(Path.GetTempPath(), $"theseus-update-{Guid.NewGuid():N}");

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public async Task FetchAsync_SavesAnInstallerThatMatchesItsChecksum()
    {
        var feed = new FakeFeed(Installer, $"{Hash(Installer)}  Theseus.msi");

        var path = await UpdateDownloader.FetchAsync(feed, Release(), _directory, CancellationToken.None);

        Assert.That(Path.GetFileName(path), Is.EqualTo("Theseus-2026.10.29.100.msi"));
        Assert.That(await File.ReadAllBytesAsync(path), Is.EqualTo(Installer));
    }

    [Test]
    public void FetchAsync_RefusesAnInstallerThatDoesNotMatch()
    {
        var feed = new FakeFeed(Installer, $"{Hash(Encoding.ASCII.GetBytes("something else"))}  Theseus.msi");

        Assert.ThrowsAsync<InvalidDataException>(() => UpdateDownloader.FetchAsync(feed, Release(), _directory, CancellationToken.None));
        Assert.That(Directory.Exists(_directory), Is.False, "nothing is written when the check fails");
    }

    [Test]
    public void FetchAsync_RefusesAReleaseWithNoChecksums()
    {
        var feed = new FakeFeed(Installer, string.Empty);

        Assert.ThrowsAsync<InvalidDataException>(() =>
            UpdateDownloader.FetchAsync(feed, Release() with { Checksums = null }, _directory, CancellationToken.None));
    }

    [Test]
    public void HashFor_ReadsTheLineForTheNamedFile()
    {
        var sums = $"{new string('a', 64)}  other.txt\n{new string('B', 64)} *Theseus.msi\n";

        Assert.That(Sha256Sums.HashFor(sums, "Theseus.msi"), Is.EqualTo(new string('b', 64)));
        Assert.That(Sha256Sums.HashFor(sums, "missing.msi"), Is.Null);
    }

    private static AvailableRelease Release() =>
        new("2026.10.29.100", Msi, Sums, new Uri("https://example.com/release"));

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private sealed class FakeFeed(byte[] installer, string sums) : IReleaseFeed
    {
        public Task<AvailableRelease?> LatestAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AvailableRelease?>(Release());

        public Task<byte[]> DownloadAsync(Uri uri, CancellationToken cancellationToken) =>
            Task.FromResult(uri == Msi ? installer : Encoding.ASCII.GetBytes(sums));
    }
}
