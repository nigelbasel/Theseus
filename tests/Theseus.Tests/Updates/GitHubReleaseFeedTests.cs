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
