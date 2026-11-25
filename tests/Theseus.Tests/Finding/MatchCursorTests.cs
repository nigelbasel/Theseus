using Theseus.Core.Finding;

namespace Theseus.Tests.Finding;

[TestFixture]
public class MatchCursorTests
{
    [Test]
    public void Forward_FromNone_IsTheFirst_AndWrapsAfterTheLast()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MatchCursor.Step(3, -1, forward: true), Is.EqualTo(0));
            Assert.That(MatchCursor.Step(3, 0, forward: true), Is.EqualTo(1));
            Assert.That(MatchCursor.Step(3, 2, forward: true), Is.EqualTo(0));
        });
    }

    [Test]
    public void Back_FromNone_IsTheLast_AndWrapsBeforeTheFirst()
    {
        Assert.Multiple(() =>
        {
            Assert.That(MatchCursor.Step(3, -1, forward: false), Is.EqualTo(2));
            Assert.That(MatchCursor.Step(3, 0, forward: false), Is.EqualTo(2));
        });
    }

    [Test]
    public void NoMatches_IsNone()
    {
        Assert.That(MatchCursor.Step(0, 1, forward: true), Is.EqualTo(-1));
    }

    [Test]
    public void AnIndexPastTheEnd_StartsAgain()
    {
        Assert.That(MatchCursor.Step(2, 5, forward: true), Is.EqualTo(0));
    }
}
