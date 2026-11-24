using System.Windows.Input;
using Theseus.App;

namespace Theseus.Tests.Windows;

/// <summary>
/// The main window's keys, as WPF builds them (baseline WB6; W27; findings B6, B7, B8): Day report on Ctrl+J, not
/// Ctrl+D, which is Delete's; the panels on F7 to F9; Ctrl+1 to Ctrl+9 and Ctrl+Tab for the project tabs only; F6 and
/// Shift+F6 between the panes.
/// </summary>
[TestFixture]
[NonParallelizable]
public class MainWindowKeyTests
{
    private static KeyBinding? Bound(MainWindow window, Key key, ModifierKeys modifiers) =>
        window.InputBindings.OfType<KeyBinding>().FirstOrDefault(binding =>
            binding.Gesture is KeyGesture gesture && gesture.Key == key && gesture.Modifiers == modifiers);

    private static void Press(MainWindow window, Key key, ModifierKeys modifiers)
    {
        KeyBinding binding = Bound(window, key, modifiers) ?? throw new AssertionException($"{modifiers}+{key} is not bound");

        Assert.That(binding.Command.CanExecute(binding.CommandParameter), Is.True, $"{modifiers}+{key} can run");
        binding.Command.Execute(binding.CommandParameter);
    }

    [Test]
    public void NoKey_IsCtrlD() =>
        MainWindowFixture.Run((window, _) => Assert.That(Bound(window, Key.D, ModifierKeys.Control), Is.Null,
            "Ctrl+D is the standard Delete key (G75, G76)"));

    [Test]
    public void CtrlJ_OpensDayReport() =>
        MainWindowFixture.Run((window, shell) =>
        {
            Press(window, Key.J, ModifierKeys.Control);

            Assert.That(shell.DayReport.OpenCommand.Runs, Has.Count.EqualTo(1));
        });

    [TestCase(Key.F7, "sessions")]
    [TestCase(Key.F8, "locks")]
    [TestCase(Key.F9, "chains")]
    public void AFunctionKey_TogglesItsPanel(Key key, string panel) =>
        MainWindowFixture.Run((window, shell) =>
        {
            Press(window, key, ModifierKeys.None);

            RecordingCommand toggled = panel switch
            {
                "sessions" => shell.ToggleSessionsPanelCommand,
                "locks" => shell.Locks.ToggleCommand,
                _ => shell.Chains.ToggleCommand,
            };
            Assert.That(toggled.Runs, Has.Count.EqualTo(1));
        });

    [Test]
    public void CtrlOne_SelectsTheFirstProjectTab_AndTogglesNoPanel() =>
        MainWindowFixture.Run((window, shell) =>
        {
            Press(window, Key.D1, ModifierKeys.Control);

            Assert.That(shell.SelectProjectTabCommand.Runs, Is.EqualTo(new object?[] { "1" }));
            Assert.That(shell.ToggleSessionsPanelCommand.Runs, Is.Empty);
            Assert.That(shell.Locks.ToggleCommand.Runs, Is.Empty);
            Assert.That(shell.Chains.ToggleCommand.Runs, Is.Empty);
        });

    [Test]
    public void CtrlOneToNine_EachSelectTheirTab() =>
        MainWindowFixture.Run((window, shell) =>
        {
            Key[] digits = [Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9];

            foreach (Key digit in digits)
            {
                Press(window, digit, ModifierKeys.Control);
            }

            Assert.That(shell.SelectProjectTabCommand.Runs, Is.EqualTo(new object?[] { "1", "2", "3", "4", "5", "6", "7", "8", "9" }));
        });

    [Test]
    public void CtrlTab_AndCtrlShiftTab_StepThroughTheTabs() =>
        MainWindowFixture.Run((window, shell) =>
        {
            Press(window, Key.Tab, ModifierKeys.Control);
            Press(window, Key.Tab, ModifierKeys.Control | ModifierKeys.Shift);

            Assert.That(shell.NextProjectTabCommand.Runs, Has.Count.EqualTo(1));
            Assert.That(shell.PreviousProjectTabCommand.Runs, Has.Count.EqualTo(1));
        });

    [Test]
    public void FSix_AndShiftFSix_MoveBetweenPanes() =>
        MainWindowFixture.Run((window, _) =>
        {
            Assert.That(Bound(window, Key.F6, ModifierKeys.None)?.Command, Is.SameAs(MainWindow.NextPaneCommand));
            Assert.That(Bound(window, Key.F6, ModifierKeys.Shift)?.Command, Is.SameAs(MainWindow.PreviousPaneCommand));
        });

    [Test]
    public void APane_GivesTheFocusToItsFirstFocusableElement() =>
        MainWindowFixture.Run((window, _) =>
        {
            System.Windows.FrameworkElement search = (System.Windows.FrameworkElement)window.FindName("SearchBar");
            System.Windows.FrameworkElement box = (System.Windows.FrameworkElement)window.FindName("SearchBox");
            System.Windows.UIElement? first = MainWindow.FirstFocusable(search);

            Assert.That(first, Is.Not.Null);
            Assert.That(box.IsAncestorOf(first!), Is.True, "the search box's text field is the top bar's first stop");
        });

    [Test]
    public void EscInTheOpenTagsFlyout_ClosesIt_AndLeavesTheSelectionAsItWas() =>
        MainWindowFixture.Run(
            (window, shell) =>
            {
                System.Windows.Controls.Primitives.ToggleButton tags = (System.Windows.Controls.Primitives.ToggleButton)window.FindName("TagsToggle");
                System.Windows.Controls.Primitives.Popup popup = (System.Windows.Controls.Primitives.Popup)window.FindName("TagsPopup");
                System.Windows.FrameworkElement flyout = (System.Windows.FrameworkElement)window.FindName("TagsFlyout");
                Theseus.App.ViewModels.WorkstreamCardViewModel card = shell.Board.Cards.Single();
                card.IsSelected = true;
                tags.IsChecked = true;

                using System.Windows.Interop.HwndSource source = new(new System.Windows.Interop.HwndSourceParameters("esc-test") { Width = 1, Height = 1 });
                KeyEventArgs escape = new(Keyboard.PrimaryDevice, source, 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
                flyout.RaiseEvent(escape);

                Assert.That(escape.Handled, Is.True, "the window's Esc, which clears the selection, never sees it");
                Assert.That(popup.IsOpen, Is.False);
                Assert.That(tags.IsChecked, Is.False);
                Assert.That(card.IsSelected, Is.True);
            },
            board => board.With("alpha"));

    [Test]
    public void Del_ArchivesTheSelection_OnTheBoardOnly() =>
        MainWindowFixture.Run((window, _) =>
        {
            System.Windows.UIElement board = (System.Windows.UIElement)window.FindName("BoardSurface");
            KeyBinding? del = board.InputBindings.OfType<KeyBinding>().SingleOrDefault(binding => binding.Key == Key.Delete);

            Assert.That(del, Is.Not.Null, "Del is on the board surface, as the menu and toolbar say (W27, B6)");
            Assert.That(System.Windows.Data.BindingOperations.GetBinding(del!, InputBinding.CommandProperty)?.Path.Path,
                Is.EqualTo("ArchiveSelectedCommand"));
            Assert.That(Bound(window, Key.Delete, ModifierKeys.None), Is.Null, "not on the window, where the project view and drawer would get it");
        });

    [Test]
    public void TheHelpPage_ListsTheNewKeys_AndNotTheOld()
    {
        string page = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Theseus.Tests.Conventions.LooseXaml.SourceFile())!, "..", "docs", "shortcuts.md"));

        Assert.That(page, Does.Contain("| Ctrl+J | Day report |"));
        Assert.That(page, Does.Contain("| F7 | Live sessions panel"));
        Assert.That(page, Does.Contain("F6, Shift+F6"));
        Assert.That(page, Does.Not.Contain("| Ctrl+D |"));
        Assert.That(page, Does.Not.Contain("| Ctrl+1 | Live sessions"));
    }
}
