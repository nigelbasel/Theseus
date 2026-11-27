using Theseus.App.ViewModels;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The tab strip's tabs (project-view-as-tabs AC 2 to 4): All projects first when 1.5b's kanban is there, unnumbered;
/// each project's tab numbered from Ctrl+1 as before, with its waits and its menu's Start and Stop.
/// </summary>
[TestFixture]
public class ProjectTabsTests
{
    [Test]
    public void AllProjects_IsAnUnnumberedTabMarkedAsSuch()
    {
        var open = new RecordingCommandStub();

        var tab = ProjectTabViewModel.AllProjects(isOpen: true, open);

        Assert.Multiple(() =>
        {
            Assert.That(tab.IsAllProjects, Is.True);
            Assert.That(tab.Title, Is.EqualTo("All projects"));
            Assert.That(tab.IsOpen, Is.True);
            Assert.That(tab.OpenCommand, Is.SameAs(open));
            Assert.That(tab.Key, Is.EqualTo("Ctrl+0"));
            Assert.That(tab.ToolTip, Does.Contain("All projects is open"));
        });
    }

    [Test]
    public void ProjectTabs_AreNotAllProjects()
    {
        var tab = new ProjectTabViewModel("theseus", "Theseus", false, null);

        Assert.That(tab.IsAllProjects, Is.False);
    }

    private sealed class RecordingCommandStub : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
        }
    }
}
