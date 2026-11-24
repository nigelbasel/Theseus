using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Start the checks… and Independent check never refuse only in the status line (Nigel, 2026-11-23: he pressed Start
/// the checks… on a project with no holdout scenarios and saw "nothing happens"). When no check can run, the Waits line
/// says why and offers the way on; a button that cannot act is disabled with its reason in plain view; a refusal found
/// only at the press is answered in a dialog.
/// </summary>
[TestFixture]
public class CheckRefusalInPlainViewTests
{
    private const string NoScenarios = "No holdout scenarios for this project yet";
    private const string Missing = "The holdout folder <root>\\holdouts is missing";

    private TestProjectsFolder _folder = null!;
    private ProjectViews _views = null!;
    private readonly List<string> _explained = [];

    [SetUp]
    public void SetUp()
    {
        _folder = TestProjectsFolder.Create();
        _views = new ProjectViews(_folder);
        _explained.Clear();
    }

    [TearDown]
    public void TearDown() => _folder.Dispose();

    private ProjectViewActions Refusing(string reason, string? wayOn, Action? ranWayOn = null) => _views.Actions with
    {
        WhyNot = (_, action) => action == StageAction.VerifyWithHoldouts ? reason : null,
        WayOn = (_, action) => action == StageAction.VerifyWithHoldouts && wayOn is not null
            ? new PieceActionViewModel(wayOn, () => ranWayOn?.Invoke(), toolTip: $"{wayOn} does it")
            : null,
    };


    [Test]
    public void ABoxsVerifyThatCannotRun_ShowsItsReasonAsVisibleText_AndInItsMenuItem()
    {
        var box = _views.Theseus(actions: Refusing(NoScenarios, "Write scenarios…")).AllNodes.Single(node => node.Name == "projects-and-method-ui");
        var verify = box.Actions.Single(action => action.Label == "Independent check…");
        MenuEntryViewModel inMenu = box.Stages.SelectMany(stage => stage.MenuEntries).First(item => item.Action.Label == "Independent check…");

        Assert.That((verify.IsEnabled, verify.Note), Is.EqualTo((false, (string?)NoScenarios)));
        Assert.That((inMenu.Header, inMenu.Note), Is.EqualTo(("Independent _check…", NoScenarios)),
            "the reason is the item's second line; its header stays fixed (W30, C10)");
    }

    [Test]
    public void AButtonThatCanRun_HasNoNote_AndItsMenuItemIsItsLabel()
    {
        var box = _views.Theseus(actions: _views.Actions with { WhyNot = (_, _) => null }).AllNodes.Single(node => node.Name == "projects-and-method-ui");
        var verify = box.Actions.Single(action => action.Label == "Independent check…");
        MenuEntryViewModel inMenu = box.Stages.SelectMany(stage => stage.MenuEntries).First(item => item.Action.Label == verify.Label);

        Assert.That((verify.Note, inMenu.Note, inMenu.Header), Is.EqualTo(((string?)null, (string?)null, "Independent _check…")));
    }

    [Test]
    public void AVerifyRefusedAtThePress_IsExplainedInADialog_NotOnlyTheStatusBar()
    {
        var notices = new List<string>();
        var card = new WorkstreamCardViewModel(ProjectCards.Card(Git22))
        {
            VerifyWithHoldoutsCommand = new RelayCommand(_ => { }, _ => false),
        };
        var actions = ProjectViewCommands.For(key => key == Git22 ? card : null,
            new RelayCommand(_ => { }), new RelayCommand(_ => { }), new RelayCommand(_ => { }),
            _ => { }, _ => { }, _ => { }, notices.Add, null, (_, _) => NoScenarios, (_, _) => null,
            (title, text) => _explained.Add($"{title}: {text}"));

        actions.VerifyWithHoldouts(Git22);

        Assert.That(_explained, Is.EqualTo(new[] { $"Can't check {card.Title} yet: {NoScenarios}." }),
            "a title saying what could not be done, not the general one (WB3's hand-on)");
        Assert.That(notices, Is.Empty);
    }

    [Test]
    public void ACheckThatCannotRun_IsALineInWaitsForYou_WithItsWayOnAsTheButton_AndCounts()
    {
        // Review of P1, blocker 1: with Start the checks… gone, a build whose check could not start said nothing at all.
        var ran = 0;
        var view = _views.BuildYourMarket(actions: Refusing(Missing, "Set the holdout folder…", () => ran++), otherWaits: pieces =>
        {
            var build = pieces.First(piece => piece.IsCurrentPiece && piece.Status.State == StageNodeState.HoldoutsNotRun);

            return [new WaitItem(WaitKind.CheckCannotRun, $"{build.Title}: its independent check cannot run. {Missing}.", [build.Name]) { CardKey = build.CardKey }];
        });
        var line = view.Waits.Single(wait => wait.Item.Kind == WaitKind.CheckCannotRun);

        line.Command!.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(line.Text, Does.EndWith($"{Missing}."));
            Assert.That((line.Button, line.ToolTip), Is.EqualTo(("Set the holdout folder…", "Set the holdout folder… does it")));
            Assert.That(ran, Is.EqualTo(1));
            Assert.That(view.Overview.Waits, Has.Some.Matches<WaitItem>(wait => wait.Kind == WaitKind.CheckCannotRun), "the sentence and the badge count it");
        });
    }

    [Test]
    public void ACheckThatCannotRun_WithNoWayOn_IsALineWithNoButton()
    {
        var view = _views.BuildYourMarket(actions: Refusing(NoScenarios, null), otherWaits: pieces =>
        {
            var build = pieces.First(piece => piece.IsCurrentPiece && piece.Status.State == StageNodeState.HoldoutsNotRun);

            return [new WaitItem(WaitKind.CheckCannotRun, "x", [build.Name]) { CardKey = build.CardKey }];
        });

        Assert.That(view.Waits.Single(wait => wait.Item.Kind == WaitKind.CheckCannotRun).HasButton, Is.False);
    }
}
