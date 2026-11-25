using Theseus.Core.Finding;

namespace Theseus.Tests.Finding;

[TestFixture]
public class SearchQueryTests
{
    [Test]
    public void Words_AreSplitOnSpace_LowerCased_AndKeptOnce()
    {
        Assert.That(SearchQuery.Parse("  BYM search bym ").Words, Is.EqualTo(new[] { "bym", "search" }));
    }

    [Test]
    public void AHashWord_IsATag_NotAWord()
    {
        var query = SearchQuery.Parse("#bym search");

        Assert.Multiple(() =>
        {
            Assert.That(query.Tags, Is.EqualTo(new[] { "bym" }));
            Assert.That(query.Words, Is.EqualTo(new[] { "search" }));
        });
    }

    [Test]
    public void ALoneHash_OrAnInvalidTag_IsDropped()
    {
        var query = SearchQuery.Parse("# #a!b");

        Assert.Multiple(() =>
        {
            Assert.That(query.Tags, Is.Empty);
            Assert.That(query.Words, Is.Empty);
            Assert.That(query.IsEmpty, Is.True);
        });
    }

    [Test]
    public void BlankText_IsEmpty()
    {
        Assert.That(SearchQuery.Parse(null).IsEmpty, Is.True);
    }
}
