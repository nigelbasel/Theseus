using System.Windows.Automation;
using System.Windows.Controls;
using Theseus.App;

namespace Theseus.Tests.Windows;

/// <summary>
/// The board's way to the project view without picking a project (board-small-changes item 3, Nigel, 2026-11-26): a
/// labelled toolbar button and View › Project view, both the shell's one command.
/// </summary>
[TestFixture]
[NonParallelizable]
public class ProjectViewCommandTests
{
    [Test]
    public void TheToolbarsProjects_RunsTheProjectViewCommand() =>
        MainWindowFixture.Run((window, shell) =>
        {
            Button projects = ((ToolBar)window.FindName("MainToolBar")).Items.OfType<Button>()
                .Single(button => AutomationProperties.GetName(button) == "Project view");

            projects.Command.Execute(null);

            Assert.That(shell.OpenProjectViewCommand.Runs, Has.Count.EqualTo(1));
            Assert.That(projects.Tag, Is.EqualTo("Open the project view on the project you last opened, or the first"));
        });

    [Test]
    public void ViewProjectView_RunsTheSameCommand() =>
        MainWindowFixture.Run((window, shell) =>
        {
            MenuItem item = (MenuItem)window.FindName("ProjectViewMenu");

            item.Command.Execute(null);

            Assert.That((item.Header, shell.OpenProjectViewCommand.Runs.Count), Is.EqualTo(("Project v_iew", 1)));
        });
}
