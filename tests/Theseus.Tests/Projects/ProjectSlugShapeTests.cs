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
}
