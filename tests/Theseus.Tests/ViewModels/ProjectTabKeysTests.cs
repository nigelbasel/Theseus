using Theseus.App;
using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>Which project tab Ctrl+1 to Ctrl+9 and Ctrl+Tab go to, and which pane F6 goes to (W27; B7, B8).</summary>
public class ProjectTabKeysTests
{
    [TestCase("1", 3, 0)]
    [TestCase("3", 3, 2)]
    [TestCase(2, 3, 1)]
    [TestCase("9", 3, 2)]
    [TestCase("9", 12, 11)]
    [TestCase("8", 12, 7)]
    public void ANumberedKey_GoesToItsTab_AndNineToTheLast(object number, int count, int expected) =>
        Assert.That(ProjectTabKeys.Numbered(number, count), Is.EqualTo(expected));

    [TestCase("4", 3)]
    [TestCase("1", 0)]
    [TestCase("0", 3)]
    [TestCase("x", 3)]
    [TestCase(null, 3)]
    public void ANumberedKey_WithNoSuchTab_GoesNowhere(object? number, int count) =>
        Assert.That(ProjectTabKeys.Numbered(number, count), Is.Null);

    [TestCase(0, 3, true, 1)]
    [TestCase(2, 3, true, 0)]
    [TestCase(0, 3, false, 2)]
    [TestCase(1, 3, false, 0)]
    public void CtrlTab_StepsRoundTheTabs(int open, int count, bool forward, int expected) =>
        Assert.That(ProjectTabKeys.Step(open, count, forward), Is.EqualTo(expected));

    [Test]
    public void CtrlTab_DoesNothing_WithNoProjectOpen_OrOnlyOneTab()
    {
        Assert.That(ProjectTabKeys.Step(null, 3, forward: true), Is.Null, "the board has the focus keys then");
        Assert.That(ProjectTabKeys.Step(0, 1, forward: true), Is.Null);
    }

    [Test]
    public void FSix_SkipsHiddenPanes_AndWraps()
    {
        bool[] shown = [true, false, true, false, true];

        Assert.That(PaneCycle.Next(shown, 0, forward: true), Is.EqualTo(2));
        Assert.That(PaneCycle.Next(shown, 4, forward: true), Is.EqualTo(0));
        Assert.That(PaneCycle.Next(shown, 0, forward: false), Is.EqualTo(4));
        Assert.That(PaneCycle.Next(shown, null, forward: true), Is.EqualTo(0));
        Assert.That(PaneCycle.Next(shown, null, forward: false), Is.EqualTo(4));
        Assert.That(PaneCycle.Next([false, false], null, forward: true), Is.Null);
    }
}
