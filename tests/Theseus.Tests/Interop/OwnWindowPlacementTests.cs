using System.Runtime.InteropServices;
using Theseus.App.Services;
using Theseus.Core.Layout;

namespace Theseus.Tests.Interop;

/// <summary>
/// The shape of the WINDOWPLACEMENT, and what happens when the window it names has gone.
///
/// `GetWindowPlacement` refuses outright unless `length` says how big the structure is, so the size
/// is part of the contract in the same way the INPUT size is. It is set with `Marshal.SizeOf`, which
/// keeps it honest; what these fix is the structure itself.
///
/// The dead-handle cases are not hypothetical. Theseus enumerates terminal windows and then acts on
/// them, and a tab can close in between — as several did on 2026-09-02. Reading a window that is no
/// longer there must come back as "no answer", not as an exception in the middle of arranging the
/// rest.
/// </summary>
[TestFixture]
public class OwnWindowPlacementTests
{
    [Test]
    public void Placement_IsTheFortyFourBytesWindowsExpects()
    {
        Assert.That(Marshal.SizeOf<OwnWindowPlacement.Placement>(), Is.EqualTo(44),
            "three ints, two POINTs and a RECT; GetWindowPlacement refuses a length that is not this");
    }

    [Test]
    public void Rect_IsFourInts()
    {
        Assert.That(Marshal.SizeOf<OwnWindowPlacement.Rect>(), Is.EqualTo(16));
    }

    [Test]
    public void Point_IsTwoInts()
    {
        Assert.That(Marshal.SizeOf<OwnWindowPlacement.Point>(), Is.EqualTo(8));
    }

    [Test]
    public void Capture_SaysNothingRatherThanGuessingWhenThereIsNoWindow()
    {
        Assert.That(OwnWindowPlacement.Capture(nint.Zero), Is.Null);
    }

    [Test]
    public void Capture_SaysNothingWhenTheWindowHasGone()
    {
        // Not a window handle, and never becomes one: the same answer a closed tab gives.
        Assert.That(OwnWindowPlacement.Capture(1), Is.Null,
            "a window that has closed must read as no answer, not as an exception");
    }

    [Test]
    public void IsMinimised_SaysNoRatherThanThrowingWhenTheWindowHasGone()
    {
        Assert.Multiple(() =>
        {
            Assert.That(OwnWindowPlacement.IsMinimised(nint.Zero), Is.False);
            Assert.That(OwnWindowPlacement.IsMinimised(1), Is.False);
        });
    }

    [Test]
    public void Apply_DoesNothingWhenThereIsNoWindow()
    {
        // Deliberately the only Apply test that runs: every other handle risks moving a real window,
        // and a test run must never touch one.
        Assert.DoesNotThrow(() =>
            OwnWindowPlacement.Apply(nint.Zero, new WindowPlacement(new LayoutRect(0, 0, 800, 600), false)));
    }

    [Test]
    public void Apply_RefusesAPlacementThatIsNotThere()
    {
        Assert.Throws<ArgumentNullException>(() => OwnWindowPlacement.Apply(nint.Zero, null!));
    }
}
