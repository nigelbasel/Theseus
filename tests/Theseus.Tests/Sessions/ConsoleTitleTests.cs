using Theseus.Core.Sessions;

namespace Theseus.Tests.Sessions;

[TestFixture]
public class ConsoleTitleTests
{
    [Test]
    public void Clean_StripsTheActivityGlyphASessionAnimatesWhileItWorks()
    {
        Assert.That(ConsoleTitle.Clean("◐ Contact form fixes"), Is.EqualTo("Contact form fixes"));
    }

    [Test]
    public void Clean_GivesTheSameAnswerWhicheverFrameTheGlyphIsOn()
    {
        Assert.That(ConsoleTitle.Clean("◑ Contact form fixes"),
            Is.EqualTo(ConsoleTitle.Clean("◐ Contact form fixes")));
    }

    [Test]
    public void Clean_LeavesATitleThatNeverHadAGlyphAlone()
    {
        Assert.That(ConsoleTitle.Clean("Job queue migration"), Is.EqualTo("Job queue migration"));
    }

    [Test]
    public void Clean_KeepsATitleThatStartsWithADigit()
    {
        Assert.That(ConsoleTitle.Clean("2026 planning"), Is.EqualTo("2026 planning"));
    }

    [Test]
    public void Clean_KeepsATitleThatIsNothingButGlyphsRatherThanReturningNothing()
    {
        Assert.That(ConsoleTitle.Clean("◐◑"), Is.EqualTo("◐◑"));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Clean_HandlesAnAbsentTitle(string? title)
    {
        Assert.That(ConsoleTitle.Clean(title), Is.Empty);
    }

    [Test]
    public void Matches_PairsAConsoleTitleWithTheTabShowingIt()
    {
        Assert.That(ConsoleTitle.Matches("◐ Contact form fixes", "Contact form fixes"), Is.True);
    }

    [Test]
    public void Matches_IgnoresCaseBecauseNeitherSideOwnsTheCasing()
    {
        Assert.That(ConsoleTitle.Matches("◐ contact form fixes", "Contact form fixes"), Is.True);
    }

    [Test]
    public void Matches_RefusesATabThatWasRenamedByHand()
    {
        Assert.That(ConsoleTitle.Matches("◑ Session recovery after shutdown", "Tailhawk"), Is.False);
    }

    [TestCase(null, "Contact form fixes")]
    [TestCase("◐ Contact form fixes", null)]
    [TestCase("", "")]
    public void Matches_RefusesToPairWhenEitherSideIsMissing(string? console, string? tab)
    {
        Assert.That(ConsoleTitle.Matches(console, tab), Is.False);
    }
}
