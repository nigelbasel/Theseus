using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Theseus.App;

namespace Theseus.Tests.Windows;

/// <summary>
/// Show active only is a toggle command on the View menu and the board's command bar, on Ctrl+K, a free Ctrl+letter key
/// (git-22 for Nigel, 2026-11-24; checklist G77, G78): the key and the menu show it, and each turns the same choice.
/// </summary>
[TestFixture]
[NonParallelizable]
public class ActiveOnlyCommandTests
{
    [Test]
    public void CtrlK_TogglesShowActiveOnly() =>
        MainWindowFixture.Run((window, shell) =>
        {
            KeyBinding ctrlK = window.InputBindings.OfType<KeyBinding>()
                .Single(binding => binding.Gesture is KeyGesture { Key: Key.K, Modifiers: ModifierKeys.Control });

            ctrlK.Command.Execute(ctrlK.CommandParameter);

            Assert.That(shell.Board.ShowsActiveOnly, Is.True);

            ctrlK.Command.Execute(ctrlK.CommandParameter);

            Assert.That(shell.Board.ShowsActiveOnly, Is.False);
        });

    [Test]
    public void TheMenuItem_AndTheCommandBarToggle_ShowTheKey_AndFollowTheChoice() =>
        MainWindowFixture.Run((window, shell) =>
        {
            MenuItem item = (MenuItem)window.FindName("ActiveOnlyMenu");
            ToggleButton toggle = (ToggleButton)window.FindName("ActiveOnlyToggle");

            shell.Board.ShowsActiveOnly = true;

            Assert.That((item.InputGestureText, item.IsCheckable, item.IsChecked), Is.EqualTo(("Ctrl+K", true, true)));
            Assert.That(toggle.IsChecked, Is.True);
            Assert.That(toggle.ToolTip, Is.EqualTo("Show active only (Ctrl+K)"));
        });
}
