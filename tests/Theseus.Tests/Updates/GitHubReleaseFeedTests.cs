using Theseus.Core.Updates;

namespace Theseus.Tests.Updates;

[TestFixture]
public class GitHubReleaseFeedTests
{
    [Test]
    public void Parse_ReadsTheVersionInstallerChecksumsAndPage()
    {
        var release = GitHubReleaseFeed.Parse(Release("v2026.10.29.21000"));

        Assert.That(release, Is.Not.Null);
        Assert.That(release!.Version, Is.EqualTo("2026.10.29.21000"));
        Assert.That(release.Installer.ToString(),
            Is.EqualTo("https://github.com/nigelbasel/Theseus/releases/download/v2026.10.29.21000/Theseus.msi"));
        Assert.That(release.Checksums!.ToString(), Does.EndWith("/SHA256SUMS.txt"));
        Assert.That(release.Page.ToString(), Is.EqualTo("https://github.com/nigelbasel/Theseus/releases/tag/v2026.10.29.21000"));
    }

    [Test]
    public void Parse_IgnoresAReleaseWithNoInstaller()
    {
        Assert.That(GitHubReleaseFeed.Parse(Release("v2026.10.29.21000", installer: false)), Is.Null);
    }

    [TestCase("draft")]
    [TestCase("prerelease")]
    public void Parse_IgnoresDraftsAndPrereleases(string flag)
    {
        Assert.That(GitHubReleaseFeed.Parse(Release("v2026.10.29.21000", flag: flag)), Is.Null);
    }

    [Test]
    public void Parse_IgnoresATagThatIsNotAVersionStamp()
    {
        Assert.That(GitHubReleaseFeed.Parse(Release("nightly")), Is.Null);
    }

    [Test]
    public void Newest_PicksTheNewestBuildStamp_PreReleasesIncluded()
    {
        var list = $"[{Release("v2026.11.19.29130")},{Release("v2026.11.20.12909", flag: "prerelease")},{Release("v2026.11.18.31498")}]";

        Assert.That(GitHubReleaseFeed.Newest(list)!.Version, Is.EqualTo("2026.11.20.12909"));
    }

    [Test]
    public void Newest_PicksAReleaseFromMainWhenItIsNewerThanEveryPreRelease()
    {
        var list = $"[{Release("v2026.11.20.12909", flag: "prerelease")},{Release("v2026.11.21.01000")}]";

        Assert.That(GitHubReleaseFeed.Newest(list)!.Version, Is.EqualTo("2026.11.21.01000"));
    }

    [Test]
    public void Newest_SkipsDraftsAndReleasesWithNoInstaller()
    {
        var list = $"[{Release("v2026.11.22.01000", flag: "draft")},{Release("v2026.11.21.01000", installer: false)},{Release("v2026.11.20.12909")}]";

        Assert.That(GitHubReleaseFeed.Newest(list)!.Version, Is.EqualTo("2026.11.20.12909"));
    }

    [TestCase("[]")]
    [TestCase("{\"message\":\"Not Found\"}")]
    public void Newest_IsNullForAnEmptyListOrNoList(string json)
    {
        Assert.That(GitHubReleaseFeed.Newest(json), Is.Null);
    }

    [Test]
    public void TheListEndpointAsksTheSameRepository()
    {
        Assert.That(GitHubReleaseFeed.ListEndpoint, Does.StartWith("https://api.github.com/repos/nigelbasel/Theseus/releases"));
        Assert.That(GitHubReleaseFeed.ListEndpoint, Is.Not.EqualTo(GitHubReleaseFeed.LatestEndpoint));
    }

    private static string Release(string tag, bool installer = true, string? flag = null)
    {
        var assets = installer
            ? $$"""
                {"name":"Theseus.msi","browser_download_url":"https://github.com/nigelbasel/Theseus/releases/download/{{tag}}/Theseus.msi"},
                {"name":"SHA256SUMS.txt","browser_download_url":"https://github.com/nigelbasel/Theseus/releases/download/{{tag}}/SHA256SUMS.txt"}
                """
            : """{"name":"notes.txt","browser_download_url":"https://example.com/notes.txt"}""";

        return $$"""
            {"tag_name":"{{tag}}","html_url":"https://github.com/nigelbasel/Theseus/releases/tag/{{tag}}",
             "draft":{{(flag == "draft" ? "true" : "false")}},"prerelease":{{(flag == "prerelease" ? "true" : "false")}},
             "assets":[{{assets}}]}
            """;
    }
}
