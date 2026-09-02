using Theseus.App.Services;

namespace Theseus.Tests.Interop;

/// <summary>
/// Reading the desktop: which terminal windows exist, and how much room a window has.
///
/// These assert what must hold whatever is on screen, because a test cannot dictate that. A work
/// area with a negative width would be accepted quietly here and would come out the far side of the
/// layout planner as windows placed off the screen, so the guard belongs at the point it is read.
///
/// **Read-only by design.** <c>TerminalFocus.FocusSession</c>, <c>FocusTerminal</c> and
/// <c>FocusContent</c> are absent on purpose: each moves keyboard focus, and a test run that steals
/// focus types the next thing Nigel writes into the wrong window.
/// </summary>
/// <remarks>
/// Categorised because these read the real desktop, which costs about six seconds. Skip them in a
/// tight loop with <c>dotnet test --filter "TestCategory!=Desktop"</c>; everything else is arithmetic
/// and runs in five.
/// </remarks>
[TestFixture]
[Category("Desktop")]
public class TerminalWindowsTests
{
    private IReadOnlyList<TerminalWindow> _windows = [];

    /// <summary>
    /// Reads the desktop once. Walking every window's tabs through UI Automation takes seconds, and
    /// doing it per test took the whole suite from five to thirty.
    /// </summary>
    [OneTimeSetUp]
    public void ReadTheDesktop()
    {
        _windows = TerminalWindows.All();
    }

    [Test]
    public void All_ReadsTheDesktopWithoutFailing()
    {
        Assert.That(_windows, Is.Not.Null);
    }

    [Test]
    public void All_DescribesEveryWindowItReturns()
    {
        foreach (var window in _windows)
        {
            Assert.Multiple(() =>
            {
                Assert.That(window.Handle, Is.Not.EqualTo(nint.Zero), "a listed window must be addressable");
                Assert.That(window.Tabs, Is.Not.Null, "empty for a window that could not be read, never null");
                Assert.That(window.Bounds.Width, Is.GreaterThanOrEqualTo(0));
                Assert.That(window.Bounds.Height, Is.GreaterThanOrEqualTo(0));
            });
        }
    }

    [Test]
    public void WorkAreaFor_NeverReportsAnAreaWithNegativeSize()
    {
        var area = TerminalWindows.WorkAreaFor(nint.Zero);

        Assert.Multiple(() =>
        {
            Assert.That(area.Width, Is.GreaterThanOrEqualTo(0),
                "a negative area becomes windows placed off the screen");
            Assert.That(area.Height, Is.GreaterThanOrEqualTo(0));
        });
    }

    [Test]
    public void WorkAreaFor_AnswersForAWindowThatHasGoneRatherThanFailing()
    {
        // Windows never issues 1 as a handle: the same answer a closed tab gives.
        Assert.That(() => TerminalWindows.WorkAreaFor(1), Throws.Nothing);
    }
}
