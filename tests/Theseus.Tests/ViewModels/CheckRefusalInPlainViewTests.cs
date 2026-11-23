using Theseus.App.ViewModels;
using Theseus.Core.Domain;
using Theseus.Core.Projects;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ProjectViewFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// Start the checks… and Verify with holdouts never refuse only in the status line (Nigel, 2026-11-23: he pressed Start
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
            ? new PieceActionViewModel(wayOn, () => ranWayOn?.Invoke(), toolTip: $"{wayOn} does it") { IsLink = true }
            : null,
        Explain = _explained.Add,
    };

    private static WaitItemViewModel ChecksWait(ProjectViewModel view) =>
        view.Waits.Single(wait => wait.Item.Kind == WaitKind.StartChecks);

    [Test]
    public void NoScenarios_TheWaitsLineSaysSo_AndOffersWriteScenarios_NotStartTheChecks()
    {
        var wrote = 0;
        var wait = ChecksWait(_views.BuildYourMarket(actions: Refusing(NoScenarios, "Write scenarios…", () => wrote++)));

        Assert.That(wait.Button, Is.EqualTo("Write scenarios…"));
        Assert.That(wait.Text, Does.Contain(NoScenarios));
        Assert.That(wait.Text, Does.StartWith("5 builds need their independent check"));

        wait.Command!.Execute(null);

        Assert.That(wrote, Is.EqualTo(1));
        Assert.That(_views.Ran, Has.None.StartsWith("Verify:"), "no verifier card is made");
        Assert.That(_views.Notices, Is.Empty);
    }

    [Test]
    public void AMissingHoldoutFolder_TheWaitsLineSaysSo_AndOffersSetTheHoldoutFolder()
    {
        var wait = ChecksWait(_views.BuildYourMarket(actions: Refusing(Missing, "Set the holdout folder…")));

        Assert.That((wait.Button, wait.ToolTip), Is.EqualTo(("Set the holdout folder…", "Set the holdout folder… does it")));
        Assert.That(wait.Text, Does.Contain(Missing));
    }

    [Test]
    public void NoCheckCanRun_AndNoWayOn_StartTheChecksIsDisabled_WithTheReasonInTheLineAndTooltip()
    {
        var wait = ChecksWait(_views.BuildYourMarket(actions: Refusing(NoScenarios, wayOn: null)));

        Assert.That(wait.Button, Is.EqualTo("Start the checks…"));
        Assert.That(wait.Command!.CanExecute(null), Is.False);
        Assert.That(wait.ToolTip, Is.EqualTo(NoScenarios));
        Assert.That(wait.Text, Does.Contain(NoScenarios));
    }

    [Test]
    public void EveryBuildBusy_StartTheChecksIsDisabled_AndTheLineSaysWhy_NotTheStatusBar()
    {
        var busy = BuildYourMarketBoard().Select(card => card with { Lifecycle = SessionLifecycle.Running }).ToList();
        var wait = ChecksWait(_views.BuildYourMarket(actions: _views.Actions with { Explain = _explained.Add }, cards: busy));

        Assert.That(wait.Command!.CanExecute(null), Is.False);
        Assert.That(wait.Text, Does.Contain("still busy"));
        Assert.That(wait.ToolTip, Does.Contain("still busy"));
    }

    [Test]
    public void ABuildThatCannotBeChecked_IsLeftOut_AndTheQuestionSaysWhich_AndWhy()
    {
        var asked = new List<string>();
        string? first = null;
        var actions = _views.Actions with
        {
            WhyNot = (key, action) => action == StageAction.VerifyWithHoldouts && key == first ? $"{key} is archived" : null,
            Ask = question => { asked.Add(question); return true; },
        };
        var wait = ChecksWait(_views.BuildYourMarket(actions: actions));
        first = BuildYourMarketCards.First(entry => wait.Item.Pieces.Contains(entry.Piece)).Card;

        wait.Command!.Execute(null);

        Assert.That(asked.Single(), Does.Contain($"{first} is archived"));
        Assert.That(_views.Ran, Has.None.EqualTo($"Verify:{first}"));
        Assert.That(_views.Ran.Count(ran => ran.StartsWith("Verify:", StringComparison.Ordinal)), Is.EqualTo(4));
    }

    [Test]
    public void NoneCanBeCheckedAnyMoreAtThePress_ItIsExplainedInADialog_NotOnlyTheStatusBar()
    {
        string? reason = null;
        var actions = _views.Actions with
        {
            WhyNot = (_, action) => action == StageAction.VerifyWithHoldouts ? reason : null,
            Explain = _explained.Add,
        };
        var wait = ChecksWait(_views.BuildYourMarket(actions: actions));
        reason = NoScenarios;

        wait.Command!.Execute(null);

        Assert.That(_explained.Single(), Does.Contain(NoScenarios));
        Assert.That(_views.Notices, Is.Empty);
        Assert.That(_views.Ran, Has.None.StartsWith("Verify:"));
    }

    [Test]
    public void ABoxsVerifyThatCannotRun_ShowsItsReasonAsVisibleText_AndInItsMenuItem()
    {
        var box = _views.Theseus(actions: Refusing(NoScenarios, "Write scenarios…")).AllNodes.Single(node => node.Name == "projects-and-method-ui");
        var verify = box.Actions.Single(action => action.Label == "Verify with holdouts…");
        var inMenu = box.Stages.SelectMany(stage => stage.MenuItems).First(item => item.Label == "Verify with holdouts…");

        Assert.That((verify.IsEnabled, verify.Note), Is.EqualTo((false, (string?)NoScenarios)));
        Assert.That(inMenu.MenuText, Does.Contain(NoScenarios));
    }

    [Test]
    public void AButtonThatCanRun_HasNoNote_AndItsMenuItemIsItsLabel()
    {
        var box = _views.Theseus(actions: _views.Actions with { WhyNot = (_, _) => null }).AllNodes.Single(node => node.Name == "projects-and-method-ui");
        var verify = box.Actions.Single(action => action.Label == "Verify with holdouts…");

        Assert.That((verify.Note, verify.MenuText), Is.EqualTo(((string?)null, "Verify with holdouts…")));
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
            _ => { }, _ => { }, _ => { }, notices.Add, null, (_, _) => NoScenarios, (_, _) => null, _explained.Add);

        actions.VerifyWithHoldouts(Git22);

        Assert.That(_explained, Is.EqualTo(new[] { $"{NoScenarios}." }));
        Assert.That(notices, Is.Empty);
    }

    [Test]
    public void TheCardMenusVerify_NamesWhyItCannotRun_InItsOwnText()
    {
        Assert.That(HoldoutChecks.MenuText(NoScenarios), Is.EqualTo($"_Verify with holdouts… — {NoScenarios}"));
        Assert.That(HoldoutChecks.MenuText(null), Is.EqualTo("_Verify with holdouts…"));
    }
}
