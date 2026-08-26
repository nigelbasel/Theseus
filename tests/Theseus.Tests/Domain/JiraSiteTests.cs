using Theseus.Core.Domain;

namespace Theseus.Tests.Domain;

[TestFixture]
public class JiraSiteTests
{
    private const string Site = "https://example.atlassian.net";

    [Test]
    public void IssueUrl_LinksAKeyToTheConfiguredInstance()
    {
        Assert.That(JiraSite.IssueUrl(Site, "APPS-2170"),
            Is.EqualTo("https://example.atlassian.net/browse/APPS-2170"));
    }

    [Test]
    public void IssueUrl_ToleratesATrailingSlashOnTheConfiguredInstance()
    {
        Assert.That(JiraSite.IssueUrl(Site + "/", "APPS-2170"),
            Is.EqualTo("https://example.atlassian.net/browse/APPS-2170"));
    }

    [Test]
    public void IssueUrl_TrimsSurroundingSpaceRatherThanBuildingABrokenLink()
    {
        Assert.That(JiraSite.IssueUrl("  " + Site + "  ", " APPS-2170 "),
            Is.EqualTo("https://example.atlassian.net/browse/APPS-2170"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void IssueUrl_GivesNoLinkWhenNoInstanceIsConfigured(string? site)
    {
        Assert.That(JiraSite.IssueUrl(site, "APPS-2170"), Is.Null);
    }

    [Test]
    public void IssueUrl_RefusesTextThatIsNotAnIssueKey()
    {
        Assert.That(JiraSite.IssueUrl(Site, "not a key"), Is.Null);
    }

    [Test]
    public void IssueUrl_RefusesAKeyThatIsReallyADatedFilename()
    {
        Assert.That(JiraSite.IssueUrl(Site, "STATUS-2026"), Is.Null);
    }

    [TestCase("example.atlassian.net")]
    [TestCase("file:///c:/dev")]
    [TestCase("javascript:alert(1)")]
    public void IssueUrl_RefusesAnInstanceThatIsNotAWebAddress(string site)
    {
        Assert.That(JiraSite.IssueUrl(site, "APPS-2170"), Is.Null);
    }
}
