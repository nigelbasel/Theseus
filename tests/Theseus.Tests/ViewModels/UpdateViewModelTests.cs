using Theseus.App.Services;
using Theseus.App.ViewModels;
using Theseus.Core.Updates;

namespace Theseus.Tests.ViewModels;

[TestFixture]
public class UpdateViewModelTests
{
    private const string Running = "2026.10.28.20000";

    [Test]
    public async Task CheckAsync_OffersANewerRelease()
    {
        var updates = Updates(Release("2026.10.29.100"));

        await updates.CheckAsync();

        Assert.That(updates.IsOffered, Is.True);
        Assert.That(updates.OfferText, Is.EqualTo("Theseus 2026.10.29.100 is available (you have 2026.10.28.20000)."));
    }

    [Test]
    public async Task CheckAsync_OffersNothingWhenThisIsTheNewest()
    {
        var updates = Updates(Release(Running));

        await updates.CheckAsync();

        Assert.That(updates.IsOffered, Is.False);
    }

    [Test]
    public async Task Later_PutsThatVersionAsideButNotANewerOne()
    {
        var feed = new Feed(Release("2026.10.29.100"));
        var updates = new UpdateViewModel(feed, Running, Path.GetTempPath(), _ => { });
        await updates.CheckAsync();

        updates.LaterCommand.Execute(null);
        await updates.CheckAsync();

        Assert.That(updates.IsOffered, Is.False);

        feed.Latest = Release("2026.10.30.100");
        await updates.CheckAsync();

        Assert.That(updates.Offer!.Version, Is.EqualTo("2026.10.30.100"));
    }

    [Test]
    public void Script_QuotesPathsSoNothingInThemIsExpanded()
    {
        var script = UpdateInstaller.Script(@"C:\Users\o'brien\$updates\Theseus.msi", @"C:\Program Files\Theseus\Theseus.exe", 4242);

        Assert.That(script, Does.Contain(@"'C:\Users\o''brien\$updates\Theseus.msi'"));
        Assert.That(script, Does.Contain(@"Start-Process -FilePath 'C:\Program Files\Theseus\Theseus.exe'"));
        Assert.That(script, Does.Contain("Wait-Process -Id 4242"));
        Assert.That(script, Does.Contain("-Verb RunAs -Wait"));
    }

    private static UpdateViewModel Updates(AvailableRelease latest) =>
        new(new Feed(latest), Running, Path.GetTempPath(), _ => { });

    private static AvailableRelease Release(string version) =>
        new(version, new Uri($"https://example.com/v{version}/Theseus.msi"), new Uri($"https://example.com/v{version}/SHA256SUMS.txt"),
            new Uri($"https://example.com/v{version}"));

    private sealed class Feed(AvailableRelease latest) : IReleaseFeed
    {
        public AvailableRelease? Latest { get; set; } = latest;

        public Task<AvailableRelease?> LatestAsync(CancellationToken cancellationToken) => Task.FromResult(Latest);

        public Task<byte[]> DownloadAsync(Uri uri, CancellationToken cancellationToken) => Task.FromResult(Array.Empty<byte>());
    }
}
