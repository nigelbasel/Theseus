using Theseus.Core.Finding;

namespace Theseus.Tests.Finding;

[TestFixture]
public class TagNamesTests
{
    [TestCase("Security", "security")]
    [TestCase("waiting on dpo", "waiting-on-dpo")]
    [TestCase("  BYM_2 ", "bym-2")]
    [TestCase("a--b", "a-b")]
    [TestCase("-edge-", "edge")]
    public void ATypedTag_IsStoredInLowerCaseWithHyphens(string typed, string stored)
    {
        Assert.That(TagNames.Normalise(typed), Is.EqualTo(stored));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("a/b")]
    [TestCase("#tag")]
    [TestCase("---")]
    public void AnythingButLettersDigitsAndHyphens_IsRefused(string typed)
    {
        Assert.That(TagNames.Normalise(typed), Is.Null);
    }

    [Test]
    public void ATagLongerThanTheLimit_IsRefused_SayingWhy()
    {
        var typed = new string('a', TagNames.MaximumLength + 1);

        Assert.Multiple(() =>
        {
            Assert.That(TagNames.Normalise(typed), Is.Null);
            Assert.That(TagNames.WhyNot(typed), Does.Contain($"{TagNames.MaximumLength}"));
        });
    }

    [Test]
    public void WhyNot_IsNullForAValidTag_AndSaysWhatATagIsOtherwise()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TagNames.WhyNot("security"), Is.Null);
            Assert.That(TagNames.WhyNot("a/b"), Is.EqualTo("A tag is letters, digits and hyphens"));
        });
    }

    [Test]
    public void Clean_NormalisesDropsInvalidAndRepeats_InOrder()
    {
        Assert.That(TagNames.Clean(["Bym", "bad/", "bym", "security"]), Is.EqualTo(new[] { "bym", "security" }));
    }
}
