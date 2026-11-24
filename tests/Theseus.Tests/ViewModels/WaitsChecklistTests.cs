using Theseus.App.ViewModels;
using Theseus.Core.Projects;
using Theseus.Core.Settings;
using Theseus.Tests.Projects;
using static Theseus.Tests.Projects.ScaleFixtures;

namespace Theseus.Tests.ViewModels;

/// <summary>
/// The waits checklist and its command bar (git-22 for Nigel, 2026-11-24): grouped rows, ticks that follow their group,
/// actions that say why they cannot apply, and what each writes to project.md; and every way into it from the view.
/// </summary>
[TestFixture]
[Apartment(ApartmentState.STA)]
public class WaitsChecklistTests
{
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

    private string FileText => File.ReadAllText(Path.Combine(_folder.Home(Large), "project.md"));

    private static WaitChecklistGroupViewModel Group(WaitsChecklistViewModel list, string containing) =>
        list.Groups.Single(group => group.Text.Contains(containing, StringComparison.Ordinal) && group.Whom == "For you");

    [Test]
    public void TickingAGroup_TicksItsCriteria_AndOneCriterionMakesItPartlyTicked()
    {
        var list = _views.Large().Checklist([]);
        var shared = Group(list, SharedAsk);

        shared.IsTicked = true;
        Assert.That(shared.Members.Select(member => member.IsTicked), Has.All.True);
        Assert.That(list.CountText, Does.StartWith("1 of "));

        shared.Members[0].IsTicked = false;
        Assert.That(shared.IsTicked, Is.Null, "partly ticked");
        Assert.That(shared.TickedCriteria, Has.Count.EqualTo(9));
    }

    [Test]
    public void WithNothingTicked_EveryActionIsDisabled_AndSaysWhy()
    {
        var list = _views.Large().Checklist([]);

        Assert.That(new[] { list.AnswerCommand, list.DoneCommand, list.ReassignCommand, list.AskLeadCommand, list.GoToObjectiveCommand }
            .Select(command => command.CanExecute(null)), Has.All.False);
        Assert.That(list.AnswerToolTip, Is.EqualTo("Tick at least one wait first."));
        Assert.That(list.GoToObjectiveToolTip, Is.EqualTo("Tick a wait on an objective first."));
    }

    [Test]
    public void GoToObjective_WantsOneObjective_AndSaysSoForMore()
    {
        var list = _views.Large().Checklist([]);
        var shared = Group(list, SharedAsk);

        shared.IsTicked = true;
        Assert.That((list.GoToObjectiveCommand.CanExecute(null), list.GoToObjectiveToolTip),
            Is.EqualTo((false, "The ticked waits concern more than one objective; tick one objective's.")));

        list.ClearCommand.Execute(null);
        shared.Members[1].IsTicked = true;
        Assert.That(list.GoToObjectiveCommand.CanExecute(null), Is.True);
    }

    [Test]
    public void Answer_RecordsOneDecision_ForEveryTickedWait_AndClearsThem()
    {
        var view = _views.Large();
        var list = view.Checklist([]);
        var finished = false;
        list.Finished += (_, _) => finished = true;
        Group(list, SharedAsk).IsTicked = true;
        Group(list, OwnAsk(0)).IsTicked = true;
        _views.Answers.Enqueue("Ana runs them; the threshold is 80%");

        list.AnswerCommand.Execute(null);
        var after = ProjectOverview.For(ProjectFileReader.Read(Large, FileText).File!, [], [], [], 0, ownerNames: Names);

        Assert.That(finished, Is.True, "the dialog closes; the view is rebuilt from the file");
        Assert.That(FileText, Does.Contain("| Ana runs them; the threshold is 80% (answers: "));
        Assert.That(after.Waits.Select(wait => wait.Text), Has.None.StartsWith(SharedAsk).And.None.StartsWith(OwnAsk(0)));
        Assert.That(after.Waits, Has.Count.EqualTo(Yours - 2));
        Assert.That(FileText, Does.Contain("Waiting: work: an audit store (1.3)"), "the work ask on the same line is kept");
    }

    [Test]
    public void Answer_Cancelled_WritesNothing()
    {
        var list = _views.Large().Checklist([]);
        var before = FileText;
        Group(list, SharedAsk).IsTicked = true;

        list.AnswerCommand.Execute(null);

        Assert.That(FileText, Is.EqualTo(before));
    }

    [Test]
    public void MarkDone_RecordsItDone_AndClearsIt()
    {
        var list = _views.Large().Checklist([]);
        Group(list, OwnAsk(0)).IsTicked = true;

        list.DoneCommand.Execute(null);

        Assert.That(FileText, Does.Contain($"| Marked done: {OwnAsk(0)} (objective 0.1) | owner |"));
        Assert.That(FileText, Does.Not.Contain($"Waiting: owner: {OwnAsk(0)}"));
    }

    [Test]
    public void Reassign_PutsTheWaitOnSomeoneElse_SoItIsNoLongerCountedAsYours()
    {
        var list = _views.Large().Checklist([]);
        Group(list, OwnAsk(0)).IsTicked = true;
        _views.Answers.Enqueue("the lead");

        list.ReassignCommand.Execute(null);
        var after = ProjectOverview.For(ProjectFileReader.Read(Large, FileText).File!, [], [], [], 0, ownerNames: Names);

        Assert.That(FileText, Does.Contain($"Waiting: on the lead: {OwnAsk(0)}"));
        Assert.That(after.Waits, Has.Count.EqualTo(Yours - 1));
        Assert.That(after.Others.Select(wait => wait.Who), Has.Member("the lead"));
    }

    [Test]
    public void AskTheLead_GivesItTheTickedWaitsAsItsPrompt_AndBringsItForward()
    {
        var list = _views.Large().Checklist([]);
        Group(list, OwnAsk(0)).IsTicked = true;

        list.AskLeadCommand.Execute(null);

        Assert.That(_views.Told.Single(), Does.StartWith("large-lead: The owner asks you about these waits in Large.").And.Contain($"- {OwnAsk(0)} ("));
        Assert.That(_views.Ran, Has.Member("Focus:large-lead"));
    }

    [Test]
    public void AskTheLead_WithNoLead_IsDisabled_AndSaysWhy()
    {
        var list = _views.Large(lead: null).Checklist([]);
        Group(list, OwnAsk(0)).IsTicked = true;

        Assert.That((list.AskLeadCommand.CanExecute(null), list.AskLeadToolTip),
            Is.EqualTo((false, "This project has no lead card on the board.")));
    }

    [Test]
    public void SelectAll_AndClear_TickAndUntickEverything()
    {
        var list = _views.Large().Checklist([]);

        list.SelectAllCommand.Execute(null);
        Assert.That(list.CountText, Is.EqualTo($"{list.Groups.Count} of {list.Groups.Count} selected"));

        list.ClearCommand.Execute(null);
        Assert.That(list.CountText, Is.EqualTo($"0 of {list.Groups.Count} selected"));
    }

    [Test]
    public void TheBandsLine_AnObjectivesBanner_TheOthersLine_AndAHeldPhase_EachOpenItWithTheirsTicked()
    {
        var view = _views.Large();
        var opened = new List<WaitsChecklistViewModel>();
        view.AllWaitsRequested += (_, list) => opened.Add(list);

        view.Waits[0].Command!.Execute(null);
        view.Phases[0].Objectives[1].RespondCommand!.Execute(null);
        view.RespondToOthersCommand.Execute(null);
        view.Phases[3].RespondCommand!.Execute(null);

        string[] Ticked(WaitsChecklistViewModel list) => [.. list.Groups.Where(group => group.IsAnyTicked).Select(group => group.Text)];

        Assert.That(Ticked(opened[0]), Is.EqualTo(new[] { view.Waits[0].Text }));
        Assert.That(Ticked(opened[1]), Is.EquivalentTo(view.Phases[0].Objectives[1].Waits.Select(wait => wait.Text)));
        Assert.That(Ticked(opened[2]), Is.EquivalentTo(view.OtherWaits.Select(wait => wait.Text)));
        Assert.That(Ticked(opened[3]), Is.EquivalentTo(view.Phases[3].Held.Select(wait => wait.Text)));
    }

    [Test]
    public void ALaterPhase_HoldsItsWaits_UnderItsHeading()
    {
        var view = _views.Large();

        Assert.That(view.Phases[0].Held, Is.Empty);
        Assert.That(view.Phases[2].HeldText, Is.EqualTo("When this phase starts, 9 things wait: 7 for you and 2 on others."));
    }

    [TestCase(ProjectViewGrouping.Objectives)]
    [TestCase(ProjectViewGrouping.Flow)]
    public void TheWaitingChip_FiltersToAsManyThingsAsItSays_InEitherLens(ProjectViewGrouping lens)
    {
        _views.Settings.Write(_views.Settings.Read() with
        {
            ProjectGrouping = new Dictionary<string, ProjectViewGrouping>(StringComparer.OrdinalIgnoreCase) { [Large] = lens },
        });
        var view = _views.Large();
        var chip = view.Counts.Single(each => each.State == StageNodeState.WaitingOnYou);

        chip.Command!.Execute(null);

        Assert.That(chip.Count, Is.EqualTo(Yours));
        Assert.That(view.NoPieceWaits, Has.Count.EqualTo(chip.Count), "none of these has a piece of work, so all are listed at the top");

        if (lens == ProjectViewGrouping.Objectives)
        {
            Assert.That(view.AllObjectives.Where(objective => objective.IsShown).Select(objective => objective.Objective!.Number),
                Is.EqualTo(new[] { "0.1", "0.2", "0.3", "0.4", "0.5" }), "the objectives the waits concern show, though they have no pieces");
        }
    }

    [Test]
    public void Reassign_ToAWhomTheFileCannotHold_AsksAgain_AndWritesTheNextAnswer()
    {
        var list = _views.Large().Checklist([]);
        Group(list, OwnAsk(0)).IsTicked = true;
        _views.Answers.Enqueue("Legal: Ana");
        _views.Answers.Enqueue("Legal");

        list.ReassignCommand.Execute(null);

        Assert.That(_views.Asked, Is.EqualTo(new[] { "Reassign to", "Reassign to" }));
        Assert.That(FileText, Does.Contain($"Waiting: on Legal: {OwnAsk(0)}").And.Not.Contain("on Legal: Ana"));
    }

    [Test]
    public void ACriterionsLine_SaysWhomEachAskIsOn_AsTheBandDoes()
    {
        var view = _views.Large();
        var criteria = view.Phases[0].Objectives[0].Criteria;

        Assert.That(criteria.Select(criterion => criterion.Waiting), Has.Member($"Waiting for you: {SharedAsk}; Waiting on work: an audit store (1.3)")
            .And.Member($"Waiting on {Team}: an answer"));
        Assert.That(view.Phases[0].Objectives[1].Criteria.Select(criterion => criterion.Waiting), Has.Member($"Waiting on {LegacyTeam}: the EC answers"));
    }

    [Test]
    public void AnAskAddedInALaterPhase_ChangesWhatTheViewDraws()
    {
        var before = _views.Large();
        var path = Path.Combine(_folder.Home(Large), "project.md");
        File.WriteAllText(path, File.ReadAllText(path).Replace("Waiting: work: the routes built\n\n**Pipeline:** feature\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n\n### Objective 6.5",
            "Waiting: work: the routes built; owner: a new ask\n\n**Pipeline:** feature\n| Workstream | Slug | Branch | Repos |\n|---|---|---|---|\n\n### Objective 6.5", StringComparison.Ordinal));
        var after = _views.Large(previous: before, write: false);

        Assert.That(after.Phases[6].Held.Count, Is.EqualTo(before.Phases[6].Held.Count + 1));
        Assert.That(after.Signature, Is.Not.EqualTo(before.Signature), "a refresh redraws for a held wait");
    }
}
