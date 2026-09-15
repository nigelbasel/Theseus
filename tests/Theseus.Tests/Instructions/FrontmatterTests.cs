using Theseus.Core.Instructions;

namespace Theseus.Tests.Instructions;

[TestFixture]
public class FrontmatterTests
{
    [Test]
    public void Read_GivesPlainAndQuotedValues()
    {
        var fields = Frontmatter.Read("---\nname: deploy\ndescription: \"Ships it\"\n---\n# Body");

        Assert.That(fields["name"], Is.EqualTo("deploy"));
        Assert.That(fields["description"], Is.EqualTo("Ships it"));
    }

    [Test]
    public void Read_JoinsAListIntoOneValue()
    {
        var fields = Frontmatter.Read("---\npaths:\n  - \"src/**/*.ts\"\n  - lib/**/*.ts\n---\n");

        Assert.That(fields["paths"], Is.EqualTo("src/**/*.ts, lib/**/*.ts"));
    }

    [Test]
    public void Read_JoinsFoldedText()
    {
        var fields = Frontmatter.Read("---\ndescription: >\n  First line\n  second line\n---\n");

        Assert.That(fields["description"], Is.EqualTo("First line second line"));
    }

    [Test]
    public void Read_HandlesWindowsLineEndings()
    {
        Assert.That(Frontmatter.Read("---\r\nname: deploy\r\n---\r\n")["name"], Is.EqualTo("deploy"));
    }

    [TestCase("# No front matter at all")]
    [TestCase("---\nname: never closed\n")]
    [TestCase("")]
    public void Read_GivesNothingWithoutACompleteBlock(string text)
    {
        Assert.That(Frontmatter.Read(text), Is.Empty);
    }
}
