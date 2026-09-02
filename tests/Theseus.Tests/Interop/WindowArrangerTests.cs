using Theseus.App.Services;
using Theseus.Core.Layout;

namespace Theseus.Tests.Interop;

/// <summary>
/// The bookkeeping around arranging windows: what moved, what refused, and what can be undone.
///
/// The arithmetic that decides where windows should go lives in <c>Theseus.Core.Layout</c> and is
/// tested there. What was never covered is this half — the counting, and the record of where things
/// were before — which is what the user is told and what an undo depends on.
///
/// Every window here is a handle that Windows will never issue, so each move is refused. That is the
/// case worth testing anyway: a tab can close between being listed and being moved, and the arranger
/// must count that as refused and carry on rather than abandon the rest.
///
/// Nothing in this fixture may be given a real window handle. Moving one of Nigel's windows from a
/// test run is exactly the kind of thing that went wrong on 2026-09-02.
/// </summary>
[TestFixture]
public class WindowArrangerTests
{
    [Test]
    public void CanRestore_IsFalseBeforeAnythingHasBeenArranged()
    {
        Assert.That(new WindowArranger().CanRestore, Is.False);
    }

    [Test]
    public void Apply_CountsNothingWhenGivenNothing()
    {
        Assert.That(new WindowArranger().Apply([]), Is.EqualTo((0, 0)));
    }

    [Test]
    public void Apply_CountsAWindowThatHasGoneAsRefusedRatherThanFailing()
    {
        var arranger = new WindowArranger();

        var (moved, refused) = arranger.Apply([(Gone(), new LayoutRect(0, 0, 800, 600))]);

        Assert.Multiple(() =>
        {
            Assert.That(moved, Is.Zero);
            Assert.That(refused, Is.EqualTo(1), "a closed tab must not stop the rest being arranged");
        });
    }

    [Test]
    public void Apply_KeepsArrangingAfterOneWindowRefuses()
    {
        var arranger = new WindowArranger();

        var (_, refused) = arranger.Apply(
        [
            (Gone(1), new LayoutRect(0, 0, 800, 600)),
            (Gone(2), new LayoutRect(800, 0, 800, 600)),
            (Gone(3), new LayoutRect(0, 600, 800, 600)),
        ]);

        Assert.That(refused, Is.EqualTo(3), "every one is attempted, not just the first");
    }

    [Test]
    public void Apply_RemembersWhereAWindowWasEvenWhenTheMoveIsRefused()
    {
        var arranger = new WindowArranger();

        arranger.Apply([(Gone(), new LayoutRect(0, 0, 800, 600))]);

        Assert.That(arranger.CanRestore, Is.True,
            "the position is recorded before the move is tried, so a partial arrangement can be undone");
    }

    [Test]
    public void Apply_RefusesAListThatIsNotThere()
    {
        Assert.Throws<ArgumentNullException>(() => new WindowArranger().Apply(null!));
    }

    [Test]
    public void Forget_DropsTheUndoWithoutMovingAnything()
    {
        var arranger = new WindowArranger();

        arranger.Apply([(Gone(), new LayoutRect(0, 0, 800, 600))]);
        arranger.Forget();

        Assert.That(arranger.CanRestore, Is.False);
    }

    [Test]
    public void RestoreAll_ForgetsEvenWhenNothingCouldBePutBack()
    {
        var arranger = new WindowArranger();

        arranger.Apply([(Gone(), new LayoutRect(0, 0, 800, 600))]);

        Assert.Multiple(() =>
        {
            Assert.That(arranger.RestoreAll(), Is.Zero, "the window is gone, so nothing moves");
            Assert.That(arranger.CanRestore, Is.False, "and it is not offered a second time");
        });
    }

    /// <param name="handle">
    /// A number Windows never issues as a window handle, so the move is always refused and no real
    /// window can be touched.
    /// </param>
    private static TerminalWindow Gone(nint handle = 1) =>
        new(handle, new LayoutRect(100, 100, 640, 480), IsMinimised: false, []);
}
