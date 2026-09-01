using Theseus.Core.Terminal;

namespace Theseus.Tests.Terminal;

[TestFixture]
public class WindowSearchOrderTests
{
    [Test]
    public void PreferringFirst_TriesTheWindowTheProcessPointsAtBeforeTheRest()
    {
        Assert.That(WindowSearchOrder.PreferringFirst(20, [10, 20, 30]), Is.EqualTo(new nint[] { 20, 10, 30 }),
            "it is the right window whenever there is only one, which is most of the time");
    }

    [Test]
    public void PreferringFirst_NamesEveryWindowExactlyOnce()
    {
        var order = WindowSearchOrder.PreferringFirst(30, [10, 20, 30]);

        Assert.That(order, Is.EquivalentTo(new nint[] { 10, 20, 30 }));
        Assert.That(order, Has.Count.EqualTo(3), "a window searched twice is a tab selected twice");
    }

    [Test]
    public void PreferringFirst_IgnoresAPreferenceThatIsNotOnTheDesktop()
    {
        Assert.That(WindowSearchOrder.PreferringFirst(99, [10, 20]), Is.EqualTo(new nint[] { 10, 20 }),
            "the window closed between being found and being searched");
    }

    [Test]
    public void PreferringFirst_CopesWithNoPreferenceAtAll()
    {
        Assert.That(WindowSearchOrder.PreferringFirst(null, [10, 20]), Is.EqualTo(new nint[] { 10, 20 }),
            "a session whose terminal cannot be traced is still findable by its tab");
    }

    [Test]
    public void PreferringFirst_CopesWithAnEmptyDesktop()
    {
        Assert.That(WindowSearchOrder.PreferringFirst(10, []), Is.Empty);
        Assert.That(WindowSearchOrder.PreferringFirst(null, []), Is.Empty);
    }
}
