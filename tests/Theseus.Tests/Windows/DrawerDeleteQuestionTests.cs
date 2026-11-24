using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Theseus.App;

namespace Theseus.Tests.Windows;

/// <summary>
/// The drawer's delete question (baseline WB7; finding B-m3; W9, W43): it starts on Keep, Esc keeps the card, the action
/// comes first and Keep last, and Tab reaches it after the drawer's tabs. Focus is read as the window's logical focus,
/// which the window is never shown to turn into keyboard focus.
/// </summary>
[TestFixture]
[NonParallelizable]
public class DrawerDeleteQuestionTests
{
    private static void Ask(MainWindow window, FakeShell shell)
    {
        shell.Drawer.Show(shell.Board.Cards.Single(card => card.Key == "alpha"));
        shell.Drawer.DeleteCommand.Execute(null);
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.Background);
    }

    [Test]
    public void AskingToDelete_FocusesKeep() =>
        MainWindowFixture.Run(
            (window, shell) =>
            {
                Ask(window, shell);

                object keep = window.FindName("KeepCard");

                Assert.That(keep, Is.InstanceOf<Button>(), "the question has its Keep button");
                Assert.That(FocusManager.GetFocusedElement(window), Is.SameAs(keep));
            },
            board => board.With("alpha"));

    [Test]
    public void Escape_InTheQuestion_KeepsTheCard() =>
        MainWindowFixture.Run(
            (window, shell) =>
            {
                Ask(window, shell);
                Border question = (Border)window.FindName("DeleteQuestion");
                KeyBinding escape = question.InputBindings.OfType<KeyBinding>().Single(binding => binding.Key == Key.Escape);

                escape.Command.Execute(escape.CommandParameter);

                Assert.That(shell.Drawer.IsConfirmingDelete, Is.False);
            },
            board => board.With("alpha"));

    [Test]
    public void TheAction_ComesFirst_AndKeepLast() =>
        MainWindowFixture.Run((window, _) =>
        {
            Button delete = (Button)window.FindName("DeleteForGood");
            Panel footer = (Panel)delete.Parent;

            Assert.That(footer.Children.IndexOf(delete), Is.LessThan(footer.Children.IndexOf((UIElement)window.FindName("KeepCard"))));
        });

    [Test]
    public void TheQuestion_ComesAfterTheTabs_InReadingOrder() =>
        MainWindowFixture.Run((window, _) =>
        {
            FrameworkElement question = (FrameworkElement)window.FindName("DeleteQuestion");
            FrameworkElement drawer = (FrameworkElement)window.FindName("DrawerPane");
            List<FrameworkElement> order = [.. MainWindowFixtureTests.Descendants<FrameworkElement>(drawer)
                .Where(element => element == question || element is TabControl)];

            Assert.That(order, Has.Count.EqualTo(2));
            Assert.That(order[0], Is.InstanceOf<TabControl>(), "the tabs are declared first, so Tab reaches them first (G65)");
        });
}
