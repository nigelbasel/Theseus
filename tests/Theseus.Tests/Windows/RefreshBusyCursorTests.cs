using System.Windows.Controls;
using System.Windows.Input;
using Theseus.App;

namespace Theseus.Tests.Windows;

/// <summary>
/// While Refresh runs the window shows the busy cursor over every control, and its toolbar button is off until it ends
/// (board-small-changes item 1, Nigel, 2026-11-26).
/// </summary>
[TestFixture]
[NonParallelizable]
public class RefreshBusyCursorTests
{
    [Test]
    public void WhileRefreshRuns_TheWindowShowsTheBusyCursor_OverItsControls() =>
        MainWindowFixture.Run((window, shell) =>
        {
            shell.RefreshCommand.Execute(null);

            Assert.That((window.Cursor, window.ForceCursor), Is.EqualTo((Cursors.Wait, true)));
        });

    [Test]
    public void OnceRefreshEnds_TheCursorIsTheControlsOwnAgain() =>
        MainWindowFixture.Run((window, shell) =>
        {
            shell.RefreshCommand.Execute(null);
            shell.RefreshCheck.SetResult("Theseus is up to date.");
            LetTheDispatcherRun();

            Assert.That((window.Cursor, window.ForceCursor), Is.EqualTo(((Cursor?)null, false)));
            Assert.That(shell.Notice, Is.EqualTo("Refreshed. Theseus is up to date."));
        });

    [Test]
    public void WhileRefreshRuns_TheToolbarsRefreshIsOff() =>
        MainWindowFixture.Run((window, shell) =>
        {
            Button refresh = Toolbar(window).Single(button => AutomationName(button) == "Refresh");

            shell.RefreshCommand.Execute(null);

            Assert.That(refresh.Command.CanExecute(null), Is.False);
        });

    // The refresh's continuation is posted to the window's dispatcher, so the queue is run before the window is looked at.
    private static void LetTheDispatcherRun()
    {
        System.Windows.Threading.DispatcherFrame frame = new();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background,
            () => frame.Continue = false);
        System.Windows.Threading.Dispatcher.PushFrame(frame);
    }

    private static IEnumerable<Button> Toolbar(MainWindow window) =>
        window.FindName("MainToolBar") is ToolBar bar ? bar.Items.OfType<Button>() : [];

    private static string AutomationName(Button button) => System.Windows.Automation.AutomationProperties.GetName(button);
}
