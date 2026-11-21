using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// A button that cannot act says why and offers the way on (1.1e AC 6a; Nigel: "if a button cant do anything it should
/// clearly show as disabled"): disabled with its own reason as the tooltip, never enabled to do nothing, and the way on
/// as a link right after it.
/// </summary>
[TestFixture]
public class ButtonReasonTests
{
    private const string Reason = "No holdout scenarios for this project yet";

    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private IReadOnlyList<PieceActionViewModel> ActionsOf(ProjectViewActions actions) =>
        _views.Theseus(actions: actions).AllNodes.Single(node => node.Name == "projects-and-method-ui").Actions;

    [Test]
    public void VerifyThatCannotRun_IsDisabled_WithItsReason_AndTheWayOnFollowsItAsALink()
    {
        var ran = 0;
        var actions = ActionsOf(_views.Actions with
        {
            WhyNot = (_, action) => action == StageAction.VerifyWithHoldouts ? Reason : null,
            WayOn = (_, action) => action == StageAction.VerifyWithHoldouts
                ? new PieceActionViewModel("Write scenarios…", () => ran++) { IsLink = true }
                : null,
        });
        var verify = actions.Single(action => action.Label == "Verify with holdouts…");
        var wayOn = actions[actions.ToList().IndexOf(verify) + 1];

        Assert.That((verify.IsEnabled, verify.ToolTip, verify.Command.CanExecute(null)), Is.EqualTo((false, (string?)Reason, false)));
        Assert.That((wayOn.Label, wayOn.IsLink, wayOn.IsEnabled), Is.EqualTo(("Write scenarios…", true, true)));

        wayOn.Command.Execute(null);

        Assert.That(ran, Is.EqualTo(1));
    }

    [Test]
    public void AButtonThatCanRun_IsUnchanged_AndHasNoWayOn()
    {
        var actions = ActionsOf(_views.Actions with { WhyNot = (_, _) => null, WayOn = (_, _) => null });

        Assert.That(actions.Single(action => action.Label == "Verify with holdouts…").IsEnabled, Is.True);
        Assert.That(actions.Any(action => action.IsLink), Is.False);
    }

    [Test]
    public void TheShellsReasons_AreAskedByCard_AndACardNotOnTheBoard_SaysSo()
    {
        var card = new WorkstreamCardViewModel(ProjectCards.Card(Git22));
        var actions = ProjectViewCommands.For(key => key == Git22 ? card : null,
            new RelayCommand(_ => { }), new RelayCommand(_ => { }), new RelayCommand(_ => { }),
            _ => { }, _ => { }, _ => { }, _ => { }, null,
            (asked, action) => $"{asked.Key}:{action}", (_, _) => null);

        Assert.That(actions.WhyNot!(Git22, StageAction.Start), Is.EqualTo("git-22:Start"));
        Assert.That(actions.WhyNot!("gone", StageAction.Start), Is.EqualTo("gone is not on the board"));
    }
}
