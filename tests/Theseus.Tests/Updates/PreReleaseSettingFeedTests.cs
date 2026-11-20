using Theseus.Core.Updates;

namespace Theseus.Tests.Updates;

[TestFixture]
public class PreReleaseSettingFeedTests
{
    private const string Main = "v2026.11.19.29130";
    private const string Pre = "v2026.11.20.12909";

    [Test]
    public async Task ReadsTheSettingOnEveryCheck_OneCallEach()
    {
        var include = false;
        var handler = new RecordingHandler(uri => uri.AbsoluteUri == GitHubReleaseFeed.LatestEndpoint
            ? Release(Main, preRelease: false)
            : $"[{Release(Pre, preRelease: true)},{Release(Main, preRelease: false)}]");
        using var feed = new GitHubReleaseFeed("2026.11.18.1", () => include, handler);

        var off = await feed.LatestAsync(CancellationToken.None);
        include = true;
        var on = await feed.LatestAsync(CancellationToken.None);
        include = false;
        var offAgain = await feed.LatestAsync(CancellationToken.None);

        Assert.That(handler.Asked.Select(uri => uri.AbsoluteUri), Is.EqualTo(new[]
        {
            GitHubReleaseFeed.LatestEndpoint, GitHubReleaseFeed.ListEndpoint, GitHubReleaseFeed.LatestEndpoint,
        }));
        Assert.That(off!.Version, Is.EqualTo("2026.11.19.29130"));
        Assert.That(on!.Version, Is.EqualTo("2026.11.20.12909"));
        Assert.That(offAgain!.Version, Is.EqualTo("2026.11.19.29130"));
    }

    private static string Release(string tag, bool preRelease) =>
        $$"""
        {"tag_name":"{{tag}}","html_url":"https://github.com/nigelbasel/Theseus/releases/tag/{{tag}}","draft":false,
         "prerelease":{{(preRelease ? "true" : "false")}},
         "assets":[{"name":"Theseus.msi","browser_download_url":"https://github.com/nigelbasel/Theseus/releases/download/{{tag}}/Theseus.msi"},
                   {"name":"SHA256SUMS.txt","browser_download_url":"https://github.com/nigelbasel/Theseus/releases/download/{{tag}}/SHA256SUMS.txt"}]}
        """;
}
