using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Theseus.App;

namespace Theseus.Tests.Windows;

/// <summary>
/// The one Board / Projects toggle and the toolbar that acts on what is shown (project-view-as-tabs AC 5 to 7; Nigel,
/// 2026-11-26: "the start, stop and archive toolbar buttons are not applicable ... we should have a single toolbar button
/// that changes depending on context"). The toolbar's toggle, View's item and Ctrl+B are one command, and its words come
/// from the shell. The board shows the card commands, and a project shows the project's own.
/// </summary>
[TestFixture]
[NonParallelizable]
public class ProjectViewCommandTests
{
    [Test]
    public void TheToolbarsToggle_RunsTheToggleCommand_AndTakesItsWordsFromTheShell() =>
        MainWindowFixture.Run((window, shell) =>
        {
            Button toggle = (Button)window.FindName("ToggleViewButton");

            toggle.Command.Execute(null);

            Assert.Multiple(() =>
            {
                Assert.That(shell.ToggleViewCommand.Runs, Has.Count.EqualTo(1));
                Assert.That(AutomationProperties.GetName(toggle), Is.EqualTo("Projects"));
                Assert.That(toggle.Tag, Is.EqualTo(shell.ToggleViewToolTip));
            });
        });

    [Test]
    public void ViewsToggleItem_RunsTheSameCommand_WithCtrlB() =>
        MainWindowFixture.Run((window, shell) =>
        {
            MenuItem item = (MenuItem)window.FindName("ProjectViewMenu");

            item.Command.Execute(null);

            Assert.That((item.Header, item.InputGestureText, shell.ToggleViewCommand.Runs.Count), Is.EqualTo(("Project v_iew", "Ctrl+B", 1)));
        });

    [Test]
    public void OnTheBoard_TheCardCommandsShow_AndTheProjectsDoNot() =>
        MainWindowFixture.Run((window, _) =>
        {
            Assert.That(Shown(window), Is.SupersetOf(new[] { "Start selected", "Stop selected", "Archive selected", "Fold cards with no session" }));
            Assert.That(Shown(window), Has.None.EqualTo("Start the project").And.None.EqualTo("Stop the project").And.None.EqualTo("More project commands"));
        });

    [Test]
    public void InAProject_TheProjectsCommandsShow_AndTheCardCommandsDoNot() =>
        MainWindowFixture.Run((window, shell) =>
        {
            shell.ShowProjectView();
            window.UpdateLayout();

            Assert.That(Shown(window), Is.SupersetOf(new[] { "Start the project", "Stop the project", "More project commands", "Fold all", "Unfold all" }));
            Assert.That(Shown(window), Has.None.EqualTo("Start selected").And.None.EqualTo("Stop selected").And.None.EqualTo("Archive selected"));
        });

    [Test]
    public void TheProjectMenu_IsOnTheMenuBar_AfterWorkstream() =>
        MainWindowFixture.Run((window, _) =>
        {
            List<string> top = [.. ((Menu)LogicalTreeHelper.GetChildren(window).OfType<DockPanel>().First().Children.OfType<Menu>().First())
                .Items.OfType<MenuItem>().Select(item => (string)item.Header)];

            Assert.That(top.IndexOf("_Project"), Is.EqualTo(top.IndexOf("_Workstream") + 1));
        });

    private static List<string> Shown(Window window) =>
        [.. ((ToolBar)window.FindName("MainToolBar")).Items.OfType<ButtonBase>()
            .Where(button => button.Visibility == Visibility.Visible).Select(AutomationProperties.GetName)];
}
