using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The project view's buttons reach the shell's own commands with the right card (1.1d AC 27), through
/// <see cref="ProjectViewCommands"/>, which ShellViewModel builds the view's actions with.
/// </summary>
[TestFixture]
public class ProjectViewCommandsTests
{
    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;
    private readonly List<string> _ran = [];
    private readonly List<string> _notices = [];
    private WorkstreamCardViewModel _git22 = null!;
    private bool _canRun;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
        _canRun = true;
        _ran.Clear();
        _notices.Clear();
        _git22 = new WorkstreamCardViewModel(ProjectCards.Card(Git22))
        {
            VerifyWithHoldoutsCommand = new RelayCommand(parameter => _ran.Add($"Verify:{parameter ?? "no parameter"}")),
        };
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private RelayCommand Recording(string name) =>
        new(parameter => _ran.Add($"{name}:{(parameter as WorkstreamCardViewModel)?.Key}"), _ => _canRun);

    private ProjectViewActions Actions() => ProjectViewCommands.For(key => key == Git22 ? _git22 : null,
        Recording("Focus"), Recording("Start"), Recording("Stop"),
        spec => _ran.Add($"OpenSpec:{spec.Slug}"), spec => _ran.Add($"AcceptSpec:{spec.Slug}"),
        path => _ran.Add($"Open:{Path.GetFileName(path)}"), _notices.Add);

    private void RunEveryAction(SessionLifecycle lifecycle)
    {
        var view = _views.Theseus(lifecycle, actions: Actions());

        foreach (var action in view.AllNodes.Single(node => node.Name == "projects-and-method-ui").Actions)
        {
            action.Command.Execute(null);
        }
    }

    [Test]
    public void ACardWithASession_ItsButtonsReachOpenSpecAcceptSpecVerifyFocusStopAndOpenRecords()
    {
        RunEveryAction(SessionLifecycle.Idle);

        Assert.That(_ran, Is.EqualTo(new[]
        {
            "OpenSpec:projects-and-method-ui", "AcceptSpec:projects-and-method-ui", "Verify:no parameter", "Focus:git-22",
            "Stop:git-22", "Open:projects-and-method-ui",
        }));
        Assert.That(_notices, Is.Empty);
    }

    [Test]
    public void ACardWithNoSession_ItsStartReachesStartWithTheCard()
    {
        RunEveryAction(SessionLifecycle.NotStarted);

        Assert.That(_ran, Does.Contain("Start:git-22"));
    }

    [Test]
    public void ACommandThatCannotRun_RunsNothing_AndSaysSo()
    {
        _canRun = false;

        Actions().Start(Git22);

        Assert.That(_ran, Is.Empty);
        Assert.That(_notices, Is.EqualTo(new[] { "That cannot be done for git-22 just now." }));
    }

    [Test]
    public void ACardNotOnTheBoard_RunsNothing_AndSaysSo()
    {
        Actions().Focus("gone");

        Assert.That(_ran, Is.Empty);
        Assert.That(_notices, Is.EqualTo(new[] { "gone is not on the board." }));
    }
}
