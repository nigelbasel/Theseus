using Theseus.Core.Projects;

namespace Theseus.Tests.Projects;

/// <summary>A slug, which also names projects and records folders, is lower-case letters, digits and hyphens.</summary>
[TestFixture]
public class ProjectSlugShapeTests
{
    [TestCase("a-slug", true)]
    [TestCase("slug2", true)]
    [TestCase("A-Slug", false)]
    [TestCase("a slug", false)]
    [TestCase("-a", false)]
    [TestCase("", false)]
    public void ASlug_IsLowerCaseLettersDigitsAndHyphens(string text, bool isSlug) =>
        Assert.That(ProjectMembership.IsSlug(text), Is.EqualTo(isSlug));

    [TestCase("My project", "my-project")]
    [TestCase("  LeadPro MCP server  ", "leadpro-mcp-server")]
    [TestCase("Café Menu: v2", "cafe-menu-v2")]
    [TestCase("BYM -- Build/Your Market!", "bym-build-your-market")]
    [TestCase("!!!", "")]
    [TestCase("", "")]
    public void ATitle_SuggestsASlug(string title, string slug)
    {
        Assert.That(ProjectMembership.SlugFrom(title), Is.EqualTo(slug));
        Assert.That(slug.Length == 0 || ProjectMembership.IsSlug(slug), Is.True);
    }
}
